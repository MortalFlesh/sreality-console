namespace MF.Sreality.Console.Command

[<RequireQualifiedAccess>]
module FlatsCommand =
    open MF.ConsoleApplication
    open MF.Config
    open MF.Notification
    open MF.Storage

    let execute: ExecuteCommand = fun (input, output) ->
        output.Title "Flats command"

        let config = ".sreality.json" |> Config.parse
        let notify message =
            match config with
            | Some { Notifications = notifications } -> Whatsapp.notify notifications message
            | _ -> output.Message message

        output.Section "Send notification"
        notify "Hello from app!"
        output.Success "Done"

        ExitCode.Success
