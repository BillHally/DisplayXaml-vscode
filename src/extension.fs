module Extension

open System

open Fable
open Fable.Core
open Fable.Core.JsInterop
open Fable.Import
open Fable.Import.vscode
open Fable.Import.Node
open Fable.Import.Node.ChildProcess
open Fable.Import.JS

module Client =
    let connect (n : int) = net.connect n

    let private sendText (client : Net.Socket) (text : string) =
        text
        |> buffer.Buffer.Create
        |> client.write

    let send (client : Net.Socket) fileName (text : string) =
        if sendText client (sprintf "%s\n" fileName) then
            if sendText client (sprintf "%d\n" (text.Split('\n').Length)) then
                sendText client (sprintf "%s\n" text)
            else
                false
        else
            false                

let launchDisplayXaml directory (__context : ExtensionContext) : ChildProcess =
    window.showInformationMessage (sprintf "Running DisplayXaml in: %s" directory) |> ignore //(sprintf "launchDisplayXaml: %s %A" directory __context) |> ignore
    let options = createEmpty<ExecOptions>
    options.cwd <- Some directory
    let exePath = "c:/git/DisplayXaml/DisplayXaml/bin/Debug/net471/DisplayXaml.exe" // TODO: get from the context
    let port = 13000 // TODO: get from the context
    let args = ResizeArray<_>([| "--port"; port.ToString() |])
    
    childProcess.execFile(exePath, args, options, fun _ _ _  -> ())
    //childProcess.exec(sprintf "%s --port %d" exePath port, options, fun _ _ _  -> ())

let activate (context : ExtensionContext) =
    
    let mutable status = ""
    let mutable serverProcess : Option<ChildProcess> = None

    let reportXamlStatus, hideXamlStatus =
        let sb = window.createStatusBarItem StatusBarAlignment.Left
        sb.text <- ""
        fun () ->
            sb.text <- sprintf "[DX:C%s]" (if status = "" then "" else sprintf ": %s " status)
            sb.show()
        , sb.hide

    let reportVMStatus, hideVMStatus =
        let sb = window.createStatusBarItem StatusBarAlignment.Left
        sb.text <- ""
        fun () ->
            sb.text <- sprintf "[DX:VM%s]" (if status = "" then "" else sprintf ": %s " status)
            sb.show()
        , sb.hide

    let send =
        let mutable client = None

        let connect () =
            status <- "Connecting..."
            let newClient = Client.connect 13000
            client <- Some newClient
            status <- ""
            newClient

        let sendUsingClient c fileName x =
            if Client.send c fileName x then
                status <- ""
            else            
                // TODO: report error
                status <- "Socket failed"
                client <- None

        fun fileName x ->
            match client with
            | Some client -> sendUsingClient client fileName x
            | None ->
                let directory = path.dirname fileName

                if serverProcess.IsNone then
                    let sp = launchDisplayXaml directory context
                    sp.on(
                        "exit",
                        fun _ ->
                            window.showWarningMessage("server exited") |> ignore
                            status <- "Disconnected"
                            serverProcess <- None
                            client <- None
                        ) |> ignore
                    serverProcess <- Some sp // TODO: ensure that we don't end up with multiple copies running

                    async {
                        window.showInformationMessage "Sleeping..." |> ignore
                        do! Async.Sleep 5000
                        window.showInformationMessage "Sending..." |> ignore
                        let client = connect ()
                        sendUsingClient client fileName x
                    }
                    |> Async.StartAsPromise
                    |> ignore

    let showIfRelevant () =
        match window.activeTextEditor with
        | Some e ->
            let d = e.document

            // TODO: Always treat XAML as Control, and use it to optionally specify the ViewModel script using a naming convention
            // TODO: Optionally treat any F# script which matches the naming convention as a ViewModel script if there's a matching Control XAML file
            // TODO: Make DisplayXaml support multiple tabs, each for a given control
            let isFSharp = d.languageId = "fsharp"
            let isXaml   = d.fileName.ToLowerInvariant().EndsWith ".xaml"

            if isFSharp then
                send d.fileName (d.getText ())               
                hideXamlStatus ()
                reportVMStatus ()
            else if isXaml then
                send d.fileName (d.getText ())                
                hideVMStatus ()
                reportXamlStatus ()
            else
                hideVMStatus ()
                hideXamlStatus ()
        | _ ->
            hideVMStatus ()
            hideXamlStatus ()

    showIfRelevant ()

    let disposables : Disposable [] = [||]
    window.onDidChangeActiveTextEditor    $ (showIfRelevant, (), disposables) |> ignore
    window.onDidChangeTextEditorSelection $ (showIfRelevant, (), disposables) |> ignore
