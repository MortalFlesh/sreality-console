namespace MF.Config

type Config = {
    Notifications: MF.Notification.Whatsapp.Config
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
                Notifications = {
                    AccountSid = parsed.Notifications.AccountSid
                    AuthToken = parsed.Notifications.AuthToken
                    TwilioNumber = parsed.Notifications.TwilioNumber
                    TargetNumber = parsed.Notifications.TargetNumber
                }
            }
