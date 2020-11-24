namespace MF.Storage

//
// Common types
//

type Key = Key of string

type GetKey<'Item> = 'Item -> Key

type Storage<'Item> = {
    GetKey: GetKey<'Item>
    Serialize: 'Item -> string
    Parse: string -> 'Item option
    Save: 'Item list -> unit
    Load: unit -> 'Item list
    Clear: unit -> unit
    Title: string
}

//
// File Storage
//

[<RequireQualifiedAccess>]
module FileStorage =
    open System.IO
    open MF.Utils

    let private saveItems path serialize = function
        | [] -> ()
        | items ->
            items
            |> List.map serialize
            |> String.concat "\n"
            |> sprintf "%s\n"
            |> FileSystem.appendToFile path

    let private loadItems path parse () =
        if path |> File.Exists |> not then []
        else
            path
            |> tee (printfn "Read storage: %s")
            |> FileSystem.readLines
            |> List.choose parse

    let private clear path () =
        if path |> File.Exists then File.Delete path
        else ()

    let create path getKey serialize parse =
        {
            Title = sprintf "FileStorage<%s>" path
            GetKey = getKey
            Serialize = serialize
            Parse = parse
            Save = saveItems path serialize
            Load = loadItems path parse
            Clear = clear path
        }
