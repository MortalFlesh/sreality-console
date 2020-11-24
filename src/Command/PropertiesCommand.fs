namespace MF.Sreality.Console.Command

[<RequireQualifiedAccess>]
module PropertiesCommand =
    open System.IO
    open MF.ConsoleApplication
    open MF.Config
    open MF.Api
    open MF.Notification
    open MF.Storage
    open MF.ErrorHandling
    open MF.Utils

    type private SearchResult<'Result> =
        | Removed of 'Result
        | Current of 'Result
        | Old of 'Result
        | New of 'Result

    [<RequireQualifiedAccess>]
    module private SearchResult =

        let value = function
            | Removed value
            | Current value
            | Old value
            | New value
                -> value

        let bind f r: SearchResult<_> =
            r |> value |> f

        let oldValue = function
            | Old value -> Some value
            | _ -> None

        let newValue = function
            | New value -> Some value
            | _ -> None

    let execute: ExecuteCommand = fun (input, output) ->
        output.Title "Search Properties"

        let config =
            input
            |> Input.getOptionValue "config"
            |> Config.parse

        let fileStorage =
            match input with
            | Input.HasOption "storage" storage ->
                let storage = storage |> OptionValue.value "storage" |> Path.GetFullPath

                FileStorage.create storage
                    (Sreality.Property.id >> Key)
                    Sreality.Property.serialize
                    Sreality.Property.parse
                |> Some
            | _ -> None

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
        | Ok currentValues ->
            fileStorage
            |> Option.iter (fun storage ->
                let previousValues = storage.Load() |> List.map Old
                let currentValues = currentValues |> List.map Current

                output.Section <| sprintf "Save results [%d] to %s" currentValues.Length storage.Title

                let outputValues title values =
                    values
                    |> List.map (SearchResult.value >> Sreality.Property.id >> List.singleton)
                    |> output.Options (sprintf "%s results [%d]" title values.Length)

                let newValues =
                    currentValues
                    |> List.filterNotInBy
                        (SearchResult.value >> storage.GetKey)
                        (previousValues |> List.map (SearchResult.value >> storage.GetKey))
                    |> List.map (SearchResult.bind New)

                let removedValues =
                    previousValues
                    |> List.filterNotInBy
                        (SearchResult.value >> storage.GetKey)
                        (currentValues |> List.map (SearchResult.value >> storage.GetKey))
                    |> List.map (SearchResult.bind New)

                previousValues |> outputValues "Previous"
                currentValues |> outputValues "Current"
                newValues |> outputValues "New"
                removedValues |> outputValues "Removed"

                output.NewLine()

                storage.Clear()

                currentValues
                |> List.map SearchResult.value
                |> storage.Save

                [
                    match newValues with
                    | [] -> ()
                    | newValues -> newValues |> List.length |> sprintf "Našlo se %d nových nabídek."

                    match removedValues with
                    | [] -> ()
                    | removedValues -> removedValues |> List.length |> sprintf "%d z předchozích nabídek, už neexistuje."
                ]
                |> function
                    | [] -> ()
                    | messages ->
                        messages
                        |> String.concat "\n"
                        |> notify
            )

            if output.IsVerbose() then
                let ``---`` = [ "---"; "---"; "---"]
                // todo if verbose
                currentValues
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

        | Error errors -> errors |> List.iter output.Error

        output.Success "Done"

        ExitCode.Success
