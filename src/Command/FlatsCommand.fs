namespace MF.Sreality.Console.Command

[<RequireQualifiedAccess>]
module FlatsCommand =
    open MF.ConsoleApplication
    open MF.Notification
    open MF.Storage

    let execute: ExecuteCommand = fun io ->
        ExitCode.Success
