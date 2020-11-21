namespace MF.Sreality.Console.Command

[<RequireQualifiedAccess>]
module FlatsCommand =
    open MF.ConsoleApplication
    open MF.Config
    open MF.Api
    open MF.Notification
    open MF.Storage
    open MF.ErrorHandling

    let execute: ExecuteCommand = fun (input, output) ->
        output.Title "Flats command"

        let config = ".sreality.json" |> Config.parse

        let notify message =
            match config with
            | Some { Notifications = notifications } -> Whatsapp.notify notifications message
            | _ -> output.Message message

        let results =
            config
            |> Option.map (fun { Sreality = sreality } -> sreality)
            |> Option.defaultValue []
            |> List.map Sreality.fetchProperties
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Seq.toList
            |> Validation.ofResults
            |> Result.map List.concat

        match results with
        | Ok results ->
            // todo if verbose
            results
            |> List.map (fun property -> [
                property.SearchTitle |> sprintf "<c:gray>%s</c>"
                property.Id |> sprintf "<c:magenta>%s</c>"
                property.Name |> sprintf "<c:yellow>%s</c>"
                property.Price |> sprintf "<c:cyan>%d</c>"
            ])
            |> output.Table [ "Search"; "Id"; "Name"; "Price" ]

            notify (sprintf "Našlo se %d nabidek." results.Length)
        | Error errors -> errors |> List.iter output.Error

        output.Success "Done"

        ExitCode.Success
