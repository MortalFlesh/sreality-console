open System
open System.IO
open MF.ConsoleApplication
open MF.Sreality.Console
open MF.ErrorHandling

[<EntryPoint>]
let main argv =
    consoleApplication {
        title AssemblyVersionInformation.AssemblyProduct
        info ApplicationInfo.MainTitle
        version AssemblyVersionInformation.AssemblyVersion

        command "sreality:property" {
            Description = "Search properties on sreality."
            Help = None
            Arguments = []
            Options = []
            Initialize = None
            Interact = None
            Execute = Command.PropertiesCommand.execute
        }

        command "about" {
            Description = "Displays information about the current project."
            Help = None
            Arguments = []
            Options = []
            Initialize = None
            Interact = None
            Execute = Command.Common.about
        }
    }
    |> run argv
