namespace MF.Config

open MF.Api
open MF.Notification
open MF.Storage

type Config = {
    Sreality: Sreality.Search list
    Notifications: Whatsapp.Config option
    FileStorage: string option
    GoogleSheets: GoogleSheets.Config option
}

[<RequireQualifiedAccess>]
module Config =
    open System.IO
    open FSharp.Data

    type private ConfigSchema = JsonProvider<"schema/config.json", SampleIsList = true>

    let parse = function
        | notFound when notFound |> File.Exists |> not -> None
        | file ->
            let parsed =
                file
                |> File.ReadAllText
                |> ConfigSchema.Parse

            Some {
                Sreality =
                    parsed.Sreality
                    |> Seq.map (fun search ->
                        let search: Sreality.Search =
                            {
                                Title = search.Title
                                Parameters = search.Param
                            }
                        search
                    )
                    |> Seq.toList

                Notifications = parsed.Notifications |> Option.map (fun notifications ->
                    {
                        AccountSid = notifications.AccountSid
                        AuthToken = notifications.AuthToken
                        TwilioNumber = notifications.TwilioNumber
                        TargetNumber = notifications.TargetNumber
                    }
                )

                FileStorage = parsed.FileStorage |> Option.map (fun storage ->
                    storage.File
                )

                GoogleSheets = parsed.GoogleSheets |> Option.map (fun storage ->
                    {
                        Credentials = storage.Credentials
                        Token = storage.Token
                        SpreadsheetId = storage.SpreadsheetId
                        Tab = storage.Tab
                    }
                )
            }
