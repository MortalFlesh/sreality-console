namespace MF.Sreality.Console.Command

[<RequireQualifiedAccess>]
module FlatsCommand =
    open MF.ConsoleApplication

    let execute: ExecuteCommand = fun io ->
        ExitCode.Success
