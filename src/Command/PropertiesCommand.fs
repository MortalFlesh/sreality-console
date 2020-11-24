namespace MF.Sreality.Console.Command

[<RequireQualifiedAccess>]
module PropertiesCommand =
    open MF.ConsoleApplication
    open MF.Config
    open MF.Api
    open MF.Notification
    open MF.Storage
    open MF.ErrorHandling

    let execute: ExecuteCommand = fun (input, output) ->
        output.Title "Search Properties"

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
            let ``---`` = [ "---"; "---"; "---"]

            // todo if verbose
            results
            |> List.groupBy Sreality.Property.searchTitle
            |> List.collect (fun (searchTitle, properties) ->
                [
                    yield ``---``
                    yield [ "<c:gray>Search</c>"; sprintf "<c:cyan>%s</c>" searchTitle; sprintf "<c:magenta>%d</c>" properties.Length ]
                    yield ``---``

                    yield!
                        properties
                        |> List.map (fun property ->
                            [
                                property.Id |> sprintf "<c:magenta>%s</c>"
                                property.Name |> sprintf "<c:yellow>%s</c>"
                                property.Price |> sprintf "<c:cyan>%d</c>"
                            ]
                        )
                ]
            )
            |> output.Table [ "Id"; "Name"; "Price" ]

            notify (sprintf "Našlo se %d nabidek." results.Length)
        | Error errors -> errors |> List.iter output.Error

        output.Success "Done"

        ExitCode.Success
