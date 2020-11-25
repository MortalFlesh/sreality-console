namespace MF.Storage

[<RequireQualifiedAccess>]
module GoogleSheets =
    open MF.ErrorHandling

    open Google.Apis.Auth.OAuth2
    open Google.Apis.Sheets.v4
    open Google.Apis.Sheets.v4.Data
    open Google.Apis.Services
    open Google.Apis.Util.Store
    open System
    open System.Collections.Generic
    open System.IO
    open System.Threading

    type Config = {
        Credentials: string
        Token: string
        SpreadsheetId: string
        Tab: string
    }

    let private letters = [ "A"; "B"; "C"; "D"; "E"; "F"; "G"; "H"; "I"; "J"; "K"; "L"; "M"; "N"; "O"; "P"; "Q"; "R"; "S"; "T"; "U"; "V"; "W"; "X"; "Y"; "Z" ]

    let private createClient config =
        let scopes = [ SheetsService.Scope.Spreadsheets ]
        let applicationName = "SrealityChecker"

        use stream = File.OpenRead(config.Credentials)
        let credentials =
            GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.Load(stream).Secrets,
                scopes,
                "user",
                CancellationToken.None,
                FileDataStore(config.Token, true)
            ).Result

        credentials.RefreshTokenAsync(CancellationToken.None)
        |> Async.AwaitTask
        |> Async.map ignore
        |> Async.Start

        new SheetsService(
            BaseClientService.Initializer(
                HttpClientInitializer = credentials,
                ApplicationName = applicationName
            )
        )

    let private range tab fromCell toCell =
        sprintf "%s!%s:%s" tab fromCell toCell

    let private range2 tab (fromLetter, fromNumber) (toLetter, toNumber) =
        range tab (sprintf "%s%d" fromLetter fromNumber) (sprintf "%s%d" toLetter toNumber)

    /// Helper function to convert F# list to C# List
    let private data<'a> (values: 'a list): List<'a> =
        values |> ResizeArray<'a>

    /// Helper function to convert F# list to C# IList
    let private idata<'a> (values: 'a list): IList<'a> =
        values |> data :> IList<'a>

    let private valuesRange tab (startLetter, startNumber) (values: _ list list) =
        let toLetter =
            let toLetterIndex =
                values
                |> List.map List.length
                |> List.sortDescending
                |> List.head

            letters.[toLetterIndex - 1]

        let toObj a = a :> obj

        let range = range2 tab (startLetter, startNumber) (toLetter, startNumber + values.Length - 1)
        let values =
            values
            |> List.map (List.map toObj >> idata)
            |> data

        ValueRange (
            Range = range,
            Values = values
        )

    let private saveItems config (serialize: _ -> string) = function
        | [] -> ()
        | items ->
            let valuesRange =
                items
                |> List.choose (fun item ->
                    let row = serialize item

                    match row.Split ";" |> Seq.toList with
                    | [] -> None
                    | values -> Some values
                )
                |> valuesRange config.Tab ("A", 2)

            use service = createClient config

            let requestBody =
                BatchUpdateValuesRequest(
                    ValueInputOption = "USER_ENTERED",
                    Data = data [ valuesRange ]
                )

            let request = service.Spreadsheets.Values.BatchUpdate(requestBody, config.SpreadsheetId)

            request.Execute() |> ignore

    let private loadItems config parse () =
        try
            use service = createClient config

            let request = service.Spreadsheets.Values.Get(config.SpreadsheetId, range config.Tab "A2" "M100")
            let response = request.Execute()
            let values = response.Values

            if values |> Seq.isEmpty then []
            else
                values
                |> Seq.map (fun row ->
                    row
                    |> Seq.map (fun i -> i.ToString())
                    |> String.concat ";"
                )
                |> Seq.choose parse
                |> Seq.toList
        with _ -> []

    let private clear config () =
        ()

    /// Serialize should return values separated by ;
    /// Parse will get values separated by ;
    let create config getKey serialize parse =
        {
            Title = sprintf "GoogleSheetsStorage<%s/%s>" config.SpreadsheetId config.Tab
            GetKey = getKey
            Serialize = serialize
            Parse = parse
            Save = saveItems config serialize
            Load = loadItems config parse
            Clear = clear config
        }
