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

        let removedValue = function
            | Removed value -> Some value
            | _ -> None

        let newValue = function
            | New value -> Some value
            | _ -> None

    let private checkWithStorage output notify currentValues storage  =
        let allPreviousValues = storage.Load() |> List.map (fun (p: Sreality.Property) -> if p.Status = "Odstraněná" then Removed p else Old p)
        let previouslyRemoved = allPreviousValues |> List.choose SearchResult.removedValue |> List.map Removed
        let previousValues = allPreviousValues |> List.choose SearchResult.oldValue |> List.map Old

        let currentValues =
            currentValues
            |> List.map (fun p ->
                let k = p |> storage.GetKey

                let previousStatus =
                    match previousValues |> List.tryFind (SearchResult.value >> storage.GetKey >> (=) k) with
                    | Some previous ->
                        let p: Sreality.Property = previous |> SearchResult.value
                        p.Status
                    | _ -> ""

                Current { p with Status = previousStatus }
            )

        output.Section <| sprintf "Save results [%d] to %s" currentValues.Length storage.Title

        let outputValues title values =
            values
            |> List.map (SearchResult.value >> (fun (p: Sreality.Property) -> [ p.Id; p.Status ]))
            |> output.Options (sprintf "%s results [%d]" title values.Length)

        let newValues =
            currentValues
            |> List.filterNotInBy
                (SearchResult.value >> storage.GetKey)
                (previousValues |> List.map (SearchResult.value >> storage.GetKey))
            |> List.map (SearchResult.bind (fun p -> New { p with Status = "Nová" }))

        let removedValues =
            previousValues
            |> List.filterNotInBy
                (SearchResult.value >> storage.GetKey)
                (currentValues |> List.map (SearchResult.value >> storage.GetKey))
            |> List.map (SearchResult.bind (fun p -> Removed { p with Status = "Odstraněná" }))

        previousValues |> outputValues "Previous"
        previouslyRemoved |> outputValues "Previous Removed"
        currentValues |> outputValues "Current"
        newValues |> outputValues "New"
        removedValues |> outputValues "Removed"

        output.NewLine()

        storage.Clear()

        (
            currentValues
            // todo - move this logic to `let currentValues` so the status would be there corrected already
            |> List.map (SearchResult.value >> (fun p ->
                { p with Status = if newValues |> List.exists (SearchResult.value >> storage.GetKey >> (=) (p |> storage.GetKey)) then "Nová" else p.Status }
            ))
        )
        @ (previouslyRemoved |> List.map SearchResult.value)
        @ (removedValues |> List.map SearchResult.value)
        |> storage.Save

        let notification title values =
            [
                match values with
                | [] -> ()
                | values ->
                    yield values |> List.length |> title
                    yield!
                        values
                        |> List.groupBy (SearchResult.value >> Sreality.Property.searchTitle)
                        |> List.map (fun (searchTitle, values) -> (sprintf "- %s [%d]" searchTitle values.Length))
            ]

        [
            yield! newValues |> notification (sprintf "Našlo se %d nových nabídek.")
            yield! removedValues |> notification (sprintf "%d z předchozích nabídek, už neexistuje.")
        ]
        |> function
            | [] -> ()
            | messages ->
                messages
                |> String.concat "\n"
                |> notify

    let execute: ExecuteCommand = fun (input, output) ->

        // GoogleSheets.save "test"
        // failwithf "Done..."

        output.Title "Search Properties"

        let config =
            input
            |> Input.getOptionValue "config"
            |> Config.parse

        let fileStorage =
            let create path =
                FileStorage.create (path |> Path.GetFullPath)
                    (Sreality.Property.id >> Key)
                    Sreality.Property.serializeToJson
                    Sreality.Property.parseJson

            match input, config with
            | Input.HasOption "storage" storage, _ ->
                storage
                |> OptionValue.value "storage"
                |> create
                |> Some

            | _, Some { FileStorage = Some storage } ->
                storage
                |> create
                |> Some

            | _ -> None

        let googleSheets =
            match config with
            | Some { GoogleSheets = Some config } ->
                GoogleSheets.create config
                    (Sreality.Property.id >> Key)
                    (Sreality.Property.serializeToSeparatedValues ";")
                    (Sreality.Property.parseValues ";")
                |> Some
            | _ -> None

        let notify message =
            match config with
            | Some { Notifications = Some notifications } -> Whatsapp.notify notifications message
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
            fileStorage |> Option.iter (checkWithStorage output notify currentValues)
            googleSheets |> Option.iter (checkWithStorage output notify currentValues)

            if output.IsVerbose() then
                let ``---`` = [ "---"; "---"; "---"; "---" ]
                // todo if verbose
                currentValues
                |> List.groupBy Sreality.Property.searchTitle
                |> List.collect (fun (searchTitle, properties) ->
                    [
                        yield ``---``
                        yield [ "<c:gray>Search</c>"; sprintf "<c:cyan>%s</c>" searchTitle; ""; sprintf "<c:magenta>%d</c>" properties.Length ]
                        yield ``---``

                        yield!
                            properties
                            |> List.map (fun property ->
                                [
                                    property.Id |> sprintf "<c:magenta>%s</c>"
                                    property.Name |> sprintf "<c:yellow>%s</c>"
                                    property.Price |> sprintf "<c:cyan>%d</c>"
                                    property.Status |> sprintf "<c:dark-yellow>%s</c>"
                                ]
                            )
                    ]
                )
                |> output.Table [ "Id"; "Name"; "Price"; "Status" ]

        | Error errors -> errors |> List.iter output.Error

        output.Success "Done"

        ExitCode.Success
