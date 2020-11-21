namespace MF.Config

open MF.Api
open MF.Notification

type Config = {
    Sreality: Sreality.Search list
    Notifications: Whatsapp.Config
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

                Notifications = {
                    AccountSid = parsed.Notifications.AccountSid
                    AuthToken = parsed.Notifications.AuthToken
                    TwilioNumber = parsed.Notifications.TwilioNumber
                    TargetNumber = parsed.Notifications.TargetNumber
                }
            }
