namespace MF.Api

// See https://github.com/mecv01/sreality-client

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
        Images: string list
        Detail: string
        Labels: string list
        IsNew: bool
        HasFloorPlan: bool
        HasVideo: bool
        HasPanorama: bool
    }

    [<RequireQualifiedAccess>]
    module Property =
        open MF.Utils

        type private PropertySchema = JsonProvider<"schema/srealityProperty.json", SampleIsList = true>

        let searchTitle { SearchTitle = searchTitle } = searchTitle
        let id { Id = id } = id

        let serialize (property: Property) = property |> Serialize.toJson

        let parse (value: string) =
            try
                let p = value |> PropertySchema.Parse

                Some {
                    SearchTitle = p.SearchTitle
                    Id = p.Id |> string
                    Name = p.Name
                    Locality = p.Locality
                    Price = p.Price
                    Images = p.Images |> Seq.toList
                    Detail = p.Detail
                    Labels = p.Labels |> Seq.toList
                    IsNew = p.IsNew
                    HasFloorPlan = p.HasFloorPlan
                    HasVideo = p.HasVideo
                    HasPanorama = p.HasPanorama
                }

            with _ -> None

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
                            Images = estate.Links.Images |> Seq.map (fun i -> i.Href) |> Seq.toList
                            Detail = detailUrl id
                            Labels = estate.Labels |> Seq.toList
                            IsNew = estate.New
                            HasFloorPlan = estate.HasFloorPlan > 0
                            HasVideo = estate.HasVideo
                            HasPanorama = estate.HasPanorama > 0
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
