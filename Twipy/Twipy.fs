module program

open System
open System.Collections.Generic
open System.Xml
open Utils
open OAuth
open Status
open System.Windows.Threading
open ipy
open TwitterLimits

(**************************************)
(* wrapPanel and scroll: http://social.msdn.microsoft.com/forums/en-US/wpf/thread/02cf717c-1191-4266-b850-91b8a2716ba6 *)
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Documents

let window = WpfUtils.createXamlWindow "Twipy.xaml"
let switcher = window.FindName("switchView") :?> Button
let wrapContent = window.FindName("content") :?> WrapPanel
let imagesHolder = window.FindName("imagesHolder") :?> UIElement
let detailsHolder = window.FindName("detailsHolder") :?> UIElement
let details = window.FindName("statusDetails") :?> StackPanel
let appStateCtl = window.FindName("appState") :?> TextBlock
let commandText = window.FindName("ipyCommand") :?> TextBox
let runCommand = window.FindName("run") :?> Button
let clearScope = window.FindName("clearScope") :?> Button

let setAppStateNoAction state = ()
let setAppStateShowInUI state = WpfUtils.dispatchMessage appStateCtl (fun _ -> appStateCtl.Text <- state)
let mutable setAppState = setAppStateNoAction

DbInterface.dbAccess <- StatusDb.statusesDb

twitterLimits.Start()

// events
Twitter.NewStatusDownloaded 
        |> Event.add (fun statusInfo -> DbInterface.dbAccess.SaveStatus(statusInfo)
                                        linfop "Downloaded {0}" statusInfo
                                        setAppState (sprintf "Status downloaded %A" statusInfo))
    
let newScope() =
    let values = new Dictionary<string, obj>()
    values.["db"]   <- (StatusDb.statusesDb :> obj)
    values.["limits"] <- (twitterLimits :> obj)
    values.["h"]    <- (ScriptingHelpers.Helpers(window, details, wrapContent) :> obj)
    values.["urls"] <- (UrlDb.urlsDb :> obj)
    createScope values
let scope = ref (newScope())

(*********** options **********)
let options = Cmdline.parseCommandLine (System.Environment.GetCommandLineArgs())
if options.Help then
    let optionSet = Cmdline.getOptionset ()
    optionSet.WriteOptionDescriptions(System.Console.Out)
    exit 0

let script =
    if options.ScriptFile.IsSome then Some(System.IO.File.ReadAllText(options.ScriptFile.Value))
    else if options.Script.IsSome then options.Script
    else None

if script.IsSome then
    if options.NoGui then
        if options.OutputScript then
            printfn "Running script %s\n\n" script.Value
        engine.Execute(script.Value, !scope) |> ignore
        exit 0
    WpfUtils.dispatchMessage commandText (fun _ -> commandText.Text <- script.Value)
(******** eof options **************)

setAppState <- setAppStateShowInUI

let runUserScript text =
    linfop "Running script {0}\n\n" text
    try 
        setAppState "working ... "
        engine.Execute(text, !scope) |> ignore 
        setAppState "done"
    with ex -> 
        printfn "Unable to execute script  %s" text
        printfn "%A" ex 
        setAppState "error"

clearScope.Click.Add(fun _ ->
    scope := newScope()
)
runCommand.Click.Add(fun _ -> 
    let text = ref ""
    WpfUtils.dispatchMessage commandText (fun _ -> text := commandText.Text)
    
    async { 
      runUserScript !text
    } |> Async.Start
)
window.Loaded.Add(fun _ ->
    if options.Run then
        runUserScript (commandText.Text)
)

switcher.Click.Add(fun _ ->
  if imagesHolder.Visibility = Visibility.Visible then
    imagesHolder.Visibility <- Visibility.Collapsed
    detailsHolder.Visibility <- Visibility.Visible
  else
    imagesHolder.Visibility <- Visibility.Visible
    detailsHolder.Visibility <- Visibility.Collapsed
)

[<assembly: System.Reflection.AssemblyTitle("Twipy")>]
[<assembly: System.Runtime.InteropServices.Guid("8c34ad41-4bc6-4b4e-b791-ef0b951a94d53")>]
()
    
[<System.STAThread>]
(new Application()).Run(window) |> ignore