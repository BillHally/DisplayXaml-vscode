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
open FSharp.Collections
open Fable.Import.vscode
open Fable.Import.Node.Crypto
open System
open Fable.Import.vscodeProposed
open System.Data.Common
open Fable.Import.vscode

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

let launchDisplayXaml directory (context : ExtensionContext) : ChildProcess =
    let options = createEmpty<ExecOptions>
    options.cwd <- Some directory
    let exePath = path.resolve (sprintf "%s/bin/DisplayXaml.exe" context.extensionPath)
    //console.info (sprintf "DisplayXaml: full path to exe: %s" exePath)
    let port = 13000 // TODO: get from the context or configuration
    let args = ResizeArray<_>([| "--port"; port.ToString() |])
    
    childProcess.execFile(exePath, args, options, fun _ _ _  -> ())
    //childProcess.exec(sprintf "%s --port %d" exePath port, options, fun _ _ _  -> ())

let [<Literal>] ShowPreviewCommand      = "displayaml.showPreview"
let [<Literal>] SetXamlPreviewVMCommand = "displayaml.setXamlPreviewVM"
let [<Literal>] PlayIcon = "▶" // \u25B6
let [<Literal>] StopIcon = "⏹" // U+23F9
let [<Literal>] SocketErrorIcon = "⚠"

type QuickPickItem =
    static member Create(label, ?description, ?detail) =
        let mutable label       = label
        let mutable description = defaultArg description ""
        let mutable detail      = detail

        {
            new Fable.Import.vscode.QuickPickItem with
                member __.label       with get() = label       and set v = label       <- v
                member __.description with get() = description and set v = description <- v
                member __.detail      with get() = detail      and set v = detail      <- v
        }            

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module QuickPickOptions =
    let create placeHolder (f : QuickPickItem -> obj) =
        let mutable matchOnDescription = None
        let mutable matchOnDetail      = None
        let mutable placeHolder        = Some placeHolder
        let mutable ignoreFocusOut     = None

        {
            new Fable.Import.vscode.QuickPickOptions with
                member __.matchOnDescription with get () = matchOnDescription and set v = matchOnDescription <- v
                member __.matchOnDetail      with get () = matchOnDetail      and set v = matchOnDetail      <- v
                member __.placeHolder        with get () = placeHolder        and set v = placeHolder        <- v
                member __.ignoreFocusOut     with get () = ignoreFocusOut     and set v = ignoreFocusOut     <- v

                member __.onDidSelectItem (x : U2<_, string>) : obj =
                    match x with
                    | U2.Case1  x -> f (x :> obj :?> QuickPickItem)
                    | U2.Case2 __ -> null
        }

type ProgressMessage(?percentage, ?message) =
    let mutable message    = message
    let mutable percentage = percentage

    interface Fable.Import.vscode.ProgressMessage with
        member this.message    with get () = message    and set v = message    <- v
        member this.percentage with get () = percentage and set v = percentage <- v

type XamlFile = XamlFile of string
type ViewModelScript = ViewModelScript of string

type ConnectionStatus =
    | Connected
    | Launching
    | Disconnected
    | SocketError

let activate (context : ExtensionContext) =
    
    let mutable status = Disconnected
    let mutable xamlViewModelScripts = Map.empty

    let mutable serverProcess : Option<ChildProcess> = None
    let mutable client = None

    let getStatusStrings () =
        match status with
        | Connected    -> StopIcon       , "Connected"
        | Launching    -> ""             , "Launching..."
        | Disconnected -> PlayIcon       , "Not connected"
        | SocketError  -> SocketErrorIcon, "Socket error"

    let connect () =
        client <- Some (Client.connect 13000)
        status <- Connected

    let send (d : TextDocument) =
        match client with
        | Some c ->
            if Client.send c d.fileName (d.getText()) then
                ()
            else
                status <- SocketError
                client <- None
        | None -> ()

    let showPreview =
        let sb = window.createStatusBarItem(StatusBarAlignment.Left, -1.0) // The progress item will use priority 0, so this will be to the right of that
        sb.text <- ""
        sb.command <- Some ShowPreviewCommand

        function
        | true  ->
            let status, tooltipStatus = getStatusStrings ()

            sb.text    <- sprintf "%s Preview XAML" status
            sb.tooltip <- Some (if serverProcess.IsNone then "Click to start the XAML preview" else sprintf "DisplayXaml: %s (click to stop)" tooltipStatus) 
            sb.show ()
        | false ->
            sb.hide ()

    let showSetXamlPreviewVM =
        let sb = window.createStatusBarItem(StatusBarAlignment.Left, -2.0) // To the right of the "Preview XAML" button
        sb.text <- ""
        sb.command <- Some SetXamlPreviewVMCommand
        
        function
        | Some xamlFile ->
            let text, tooltip =
                match xamlViewModelScripts |> Map.tryFind xamlFile with
                | Some (ViewModelScript scriptFile) ->
                    sprintf "VM: [%s]" (path.basename scriptFile),
                    sprintf "Set the F# script used to create the ViewModel (currently %s)" scriptFile
                | None ->
                    sprintf "Set VM script",
                    sprintf "DisplayXaml: Set the F# script to use to create the ViewModel"

            sb.text    <- text
            sb.tooltip <- Some tooltip
            sb.show ()
        | None ->
            sb.hide ()

    let showVMStatus =
        let sb = window.createStatusBarItem StatusBarAlignment.Left
        sb.text <- ""
//        sb.command <- Some SetXamlPreviewVMCommand // TODO: select the control
        
        function
        | Some viewModelScript ->
            let status, tooltipStatus = getStatusStrings ()

            let text, tooltip =
                match xamlViewModelScripts |> Map.tryPick (fun k v -> if v = viewModelScript then Some k else None) with
                | Some (XamlFile control) ->
                    sprintf "XAML: %s %s" (path.basename control) status,
                    sprintf "DisplayXaml[%s]: this F# script is used to create the ViewModel for %s" tooltipStatus control
                | None ->
                    sprintf "XAML: <none> %s" status,
                    sprintf "DisplayXaml[%s]: this F# script is not currently used to create the ViewModel for any controls" tooltipStatus

            sb.text    <- text
            sb.tooltip <- Some tooltip
            sb.show ()
        | None ->
            sb.hide ()

    let previewXaml __ =
        match window.activeTextEditor, serverProcess with
        | Some e, None ->
            let mutable title = Some "︀\u200B" // Zero-width space (an empty string results in the progress spinner being invisible)
            window.withProgress
                (
                    {                                                       
                        new ProgressOptions with
                            member __.location with get () = ProgressLocation.Window and set _ = ()
                            member __.title    with get () = title and set v = title <- v
                    },
                    (
                        fun __ ->
                            async {
                                let xamlDocument = e.document
                                let directory = path.dirname xamlDocument.fileName
                                let sp = launchDisplayXaml directory context
                                sp.on(
                                    "exit",
                                    fun _ ->
                                        status        <- Disconnected
                                        serverProcess <- None
                                        client        <- None

                                        window.activeTextEditor
                                        |> Option.iter
                                            (
                                                fun e ->
                                                    let d = e.document
                                                    match d.languageId, path.extname (d.fileName.ToLowerInvariant()) with
                                                    | "xml", ".xaml" ->
                                                        showPreview true
                                                        showSetXamlPreviewVM (Some (XamlFile d.fileName))
                                                    | "fsharp", ".fsx" ->
                                                        xamlViewModelScripts
                                                        |> Seq.tryPick (fun kv -> if kv.Value = ViewModelScript d.fileName then Some (Some (ViewModelScript d.fileName)) else None)
                                                        |> Option.iter showVMStatus
                                                    | _, _ -> ()                                                    
                                            )
                                    ) |> ignore
                                serverProcess <- Some sp
                                status <- Launching
                                showPreview true
                                showSetXamlPreviewVM (Some (XamlFile xamlDocument.fileName))

                                // TODO: wait for the Server to tell us it is ready
                                do! Async.Sleep 3000
    
                                connect ()
    
                                // Send the XAML document
                                send xamlDocument
    
                                // Send the ViewModel script (if there is one)
                                xamlViewModelScripts
                                |> Map.tryFind (XamlFile xamlDocument.fileName)
                                |> Option.bind (fun (ViewModelScript x) -> workspace.textDocuments |> Seq.tryFind (fun d -> d.fileName = x))
                                |> Option.iter send

                                showPreview true // Ensure that the updated status is shown to the user
                            }
                            |> Async.StartAsPromise
                            |> unbox<PromiseLike<unit>>
                    )
                )                    
            |> ignore
        | _, Some server -> server.kill ""
        | None, None -> ()

        null        

    let setXamlPreviewVM __ =
        match window.activeTextEditor with
        | Some e ->
            let d = e.document
            match d.languageId, path.extname (d.fileName.ToLowerInvariant()) with
            | "xml", ".xaml" ->
                async {
                    let! files =
                        workspace.findFiles("**/*.fsx")
                        |> unbox<Promise<_>>
                        |> Async.AwaitPromise

                    let picks =
                        files
                        |> Seq.map
                            (
                                fun (x : Uri) ->
                                    let file = x.fsPath
                                    QuickPickItem.Create(path.basename file, file)
                            )                        
                        |> Seq.append [| QuickPickItem.Create "[None]" |]            
                        |> ResizeArray
                        |> U2.Case1

                    let pickOptions =
                        QuickPickOptions.create "Select VM F# script" (fun _ -> null)

                    let! pick =
                        window.showQuickPick(picks, pickOptions)
                        |> unbox<Promise<Option<vscode.QuickPickItem>>>
                        |> Async.AwaitPromise

                    let promise =
                        match pick with
                        | Some x ->
                            let xamlFile = XamlFile d.fileName

                            xamlViewModelScripts <-
                                match x.description with
                                | "" ->
                                    // The description is empty when the user selected
                                    // the "[None]" option - so remove the existing script (if there is one)
                                    xamlViewModelScripts
                                    |> Map.remove xamlFile
                                | description ->
                                    xamlViewModelScripts
                                    |> Map.add xamlFile (ViewModelScript description)

                            showSetXamlPreviewVM (Some xamlFile)

                            workspace.textDocuments
                            |> Seq.tryFind (fun d -> d.fileName = x.description)
                            |> function
                                | Some d ->
                                    async { return Some d } |> Async.StartAsPromise
                                | None ->
                                    files
                                    |> Seq.tryFind (fun f -> f.fsPath = x.description)
                                    |>
                                        (
                                            function
                                            | None -> async { return None } |> Async.StartAsPromise
                                            | Some uri ->
                                                workspace.openTextDocument(uri)
                                                |> unbox<Promise<_>>
                                        )                                            
                        | None -> async { return None } |> Async.StartAsPromise

                    let! document = Async.AwaitPromise promise
                    document |> Option.iter send
                } |> Async.StartAsPromise |> ignore
            | _, _ ->
                window.showErrorMessage (sprintf "%s is only valid for XAML documents" SetXamlPreviewVMCommand) |> ignore
        | None ->
            window.showErrorMessage (sprintf "%s is only valid for XAML documents" SetXamlPreviewVMCommand) |> ignore

        null

    commands.registerCommand (     ShowPreviewCommand, previewXaml     , null) |> ignore
    commands.registerCommand (SetXamlPreviewVMCommand, setXamlPreviewVM, null) |> ignore

    let showIfRelevant () =
        match window.activeTextEditor with
        | Some e ->
            let d = e.document

            match d.languageId, path.extname (d.fileName.ToLowerInvariant()) with
            | "xml", ".xaml" ->
                send d

                showPreview true
                showSetXamlPreviewVM (Some (XamlFile d.fileName))
                showVMStatus None
            | "fsharp", ".fsx" ->            
                xamlViewModelScripts
                |> Seq.tryFind (fun kv -> kv.Value = ViewModelScript d.fileName)
                |> Option.iter (fun _ -> send d)

                showPreview  false
                showSetXamlPreviewVM None
                showVMStatus (Some (ViewModelScript d.fileName))
            | _, _ ->
                showPreview false
                showSetXamlPreviewVM None
                showVMStatus None
        | _ ->
            showPreview false
            showSetXamlPreviewVM None
            showVMStatus None

    showIfRelevant ()

    let disposables : Disposable [] = [||]

    window.onDidChangeActiveTextEditor    $ (showIfRelevant, (), disposables) |> ignore
    window.onDidChangeTextEditorSelection $ (showIfRelevant, (), disposables) |> ignore
