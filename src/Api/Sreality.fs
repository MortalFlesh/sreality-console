namespace MF.Api

// See https://github.com/mecv01/sreality-client

open System

[<RequireQualifiedAccess>]
module Sreality =
    open FSharp.Data
    open MF.ErrorHandling

    type Search = {
        Title: string
        Parameters: string
    }

    type Property = {
        SearchTitle: string
        Id: string
        Name: string
        Locality: string
        Price: int
        Detail: string
        Labels: string list
        IsNew: bool
        HasFloorPlan: bool
        HasVideo: bool
        HasPanorama: bool
        Status: string
        UpdatedAt: DateTime
    }

    [<RequireQualifiedAccess>]
    module Property =
        open MF.Utils

        type private PropertySchema = JsonProvider<"schema/srealityProperty.json", SampleIsList = true>

        let searchTitle { SearchTitle = searchTitle } = searchTitle
        let id { Id = id } = id

        let serializeToJson (property: Property) = property |> Serialize.toJson
        let serializeToSeparatedValues separator (property: Property) =
            [
                property.SearchTitle
                property.Id
                property.Name
                property.Locality
                (property.Price |> string)
                property.Detail
                (property.Labels |> String.concat ", ")
                (if property.IsNew then "Ano" else "Ne")
                (if property.HasFloorPlan then "Ano" else "Ne")
                (if property.HasVideo then "Ano" else "Ne")
                (if property.HasPanorama then "Ano" else "Ne")
                property.Status
                property.UpdatedAt.ToUniversalTime().AddHours(1.).ToString("HH:mm dd.MM.yyyy")
            ]
            |> String.concat separator

        let parseJson (value: string) =
            try
                let p = value |> PropertySchema.Parse

                Some {
                    SearchTitle = p.SearchTitle
                    Id = p.Id |> string
                    Name = p.Name
                    Locality = p.Locality
                    Price = p.Price
                    Detail = p.Detail
                    Labels = p.Labels |> Seq.toList
                    IsNew = p.IsNew
                    HasFloorPlan = p.HasFloorPlan
                    HasVideo = p.HasVideo
                    HasPanorama = p.HasPanorama
                    Status = p.Status |> Option.defaultValue ""
                    UpdatedAt = DateTime.Now
                }

            with _ -> None

        let parseValues (separator: string) = function
            | String.IsEmpty -> None
            | value ->
                let p = { SearchTitle = ""; Id = ""; Name = ""; Locality = ""; Price = 0; Detail = ""; Labels = []; IsNew = false; HasFloorPlan = false; HasVideo = false; HasPanorama = false; Status = ""; UpdatedAt = DateTime.Now }

                match value.Split separator |> Seq.toList with
                | [ searchTitle; id; name; locality; price; detail; labels; isNew; hasFloorPlan; hasVideo; hasPanorama; status; _ (* updatedAt *) ]
                | [ searchTitle; id; name; locality; price; detail; labels; isNew; hasFloorPlan; hasVideo; hasPanorama; status ] ->
                    Some {
                        p with
                            SearchTitle = searchTitle
                            Id = id
                            Name = name
                            Locality = locality
                            Price = try price |> int with _ -> 0
                            Detail = detail
                            Labels = labels.Split ',' |> Seq.map (String.trim ' ') |> Seq.toList
                            IsNew = isNew = "Ano"
                            HasFloorPlan = hasFloorPlan = "Ano"
                            HasVideo = hasVideo = "Ano"
                            HasPanorama = hasPanorama = "Ano"
                            Status = status
                    }
                | [ searchTitle; id; name; locality; price; detail; labels; isNew; hasFloorPlan; hasVideo; hasPanorama ] ->
                    Some {
                        p with
                            SearchTitle = searchTitle
                            Id = id
                            Name = name
                            Locality = locality
                            Price = try price |> int with _ -> 0
                            Detail = detail
                            Labels = labels.Split ',' |> Seq.map (String.trim ' ') |> Seq.toList
                            IsNew = isNew = "Ano"
                            HasFloorPlan = hasFloorPlan = "Ano"
                            HasVideo = hasVideo = "Ano"
                            HasPanorama = hasPanorama = "Ano"
                    }
                | [ searchTitle; id; name; locality; price ] ->
                    Some {
                        p with
                            SearchTitle = searchTitle
                            Id = id
                            Name = name
                            Locality = locality
                            Price = try price |> int with _ -> 0
                    }
                | [ searchTitle; id ] ->
                    Some {
                        p with
                            SearchTitle = searchTitle
                            Id = id
                    }
                | _ -> None

    type private BaseOptions = {
        BaseUrl: string
        Method: string
    }

    let private baseOptions = {
        BaseUrl = "https://www.sreality.cz/api/cs/v2"
        Method = "GET"
    }

    [<RequireQualifiedAccess>]
    module private Response =
        let private detailUrl hashId = sprintf "https://www.sreality.cz/detail/1/2/3/4/%s" hashId

        type private EstatesResponseSchema = JsonProvider<"schema/estatesResponse.json", SampleIsList = true>

        let parse searchTitle response = asyncResult {
            try
                let response = response |> EstatesResponseSchema.Parse

                return
                    response.Embedded.Estates
                    |> Seq.choose (fun estate -> maybe {
                        if estate.IsAuction then return! None

                        let id = estate.HashId |> string

                        return {
                            SearchTitle = searchTitle
                            Id = id
                            Name = estate.Name
                            Locality = estate.Locality
                            Price = estate.PriceCzk.ValueRaw
                            Detail = detailUrl id
                            Labels = estate.Labels |> Seq.toList
                            IsNew = estate.New
                            HasFloorPlan = estate.HasFloorPlan > 0
                            HasVideo = estate.HasVideo
                            HasPanorama = estate.HasPanorama > 0
                            Status = ""
                            UpdatedAt = DateTime.Now
                        }
                    })
                    |> Seq.toList

            with e -> return! AsyncResult.ofError e.Message
        }

    let private fetch path { Title = title; Parameters = queryString }: AsyncResult<Property list, string> = asyncResult {
        let url = sprintf "%s%s?%s" baseOptions.BaseUrl path queryString

        let! rawResponse =
            Http.AsyncRequestString (
                url,
                httpMethod = baseOptions.Method
            )
            |> AsyncResult.ofAsyncCatch (fun e -> e.Message)

        return!
            rawResponse
            |> Response.parse title
    }

    let fetchProperties = fetch "/estates"
