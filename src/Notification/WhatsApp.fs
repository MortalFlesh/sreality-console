namespace MF.Notification

[<RequireQualifiedAccess>]
module Whatsapp =
    open Twilio
    open Twilio.Rest.Api.V2010.Account
    open Twilio.Types

    type Config = {
        AccountSid: string
        AuthToken: string
        TwilioNumber: string
        TargetNumber: string
    }

    let private whatsAppNumber number =
        PhoneNumber(sprintf "whatsapp:%s" number)

    let notify config message =
        TwilioClient.Init(config.AccountSid, config.AuthToken)

        let messageOptions =
            CreateMessageOptions(
                whatsAppNumber config.TargetNumber
            )
        messageOptions.From <- whatsAppNumber config.TwilioNumber
        messageOptions.Body <- message

        MessageResource.Create(messageOptions)
        |> ignore
