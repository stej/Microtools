﻿module program

open System
open System.Collections.Generic
open System.Xml
open Utils
open OAuth
open Status
open System.Windows.Threading
open ipy

(**************************************)
(* wrapPanel and scroll: http://social.msdn.microsoft.com/forums/en-US/wpf/thread/02cf717c-1191-4266-b850-91b8a2716ba6 *)
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Documents

let window = WpfUtils.createXamlWindow "Twipy.xaml"
let switcher = window.FindName("switchView") :?> Button
let wrap = window.FindName("content") :?> WrapPanel
let imagesHolder = window.FindName("imagesHolder") :?> UIElement
let detailsHolder = window.FindName("detailsHolder") :?> UIElement
let details = window.FindName("statusDetails") :?> StackPanel
let appStateCtl = window.FindName("appState") :?> TextBlock
let commandText = window.FindName("ipyCommand") :?> TextBox
let runCommand = window.FindName("run") :?> Button
let clearScope = window.FindName("clearScope") :?> Button

let fillPictures statuses =
    wrap.Children.Clear()
    statuses 
      |> Flatten 
      |> Seq.sortBy (fun status -> status.StatusId)
      |> Seq.map (fun status -> WpfUtils.createLittlePicture status) 
      |> Seq.iter (fun pic -> wrap.Children.Add(pic) |> ignore)
let fillDetails statuses =
    details.Children.Clear()
    statuses 
      |> Seq.sortBy (fun status -> status |> Status.GetStatusIdsForNode |> Seq.sortBy (fun statusid -> -statusid) |> Seq.nth 0)
      |> Seq.iter (
            fun status -> WpfUtils.dispatchMessage window (fun _ -> let controls = WpfUtils.createConversationControls WpfUtils.End details
                                                                    WpfUtils.setNewConversation controls status |> ignore)
        )

let setAppState state = 
    WpfUtils.dispatchMessage appStateCtl (fun _ -> appStateCtl.Text <- state)

//window.Loaded.Add()
type public Helpers () = 
    member x.loadTree status = StatusesReplies.loadSavedReplyTree status
    member x.show statuses = 
        WpfUtils.dispatchMessage window (fun _ -> fillPictures statuses; fillDetails statuses)
    member x.showAsText o =
        WpfUtils.dispatchMessage window (fun _ -> 
            wrap.Children.Clear()
            let ret = new TextBlock(TextWrapping = TextWrapping.Wrap,
                            Padding = new Thickness(0.),
                            Margin = new Thickness(5., 0., 0., 5.))
            ret.Inlines.Add(new Run(o.ToString()))
            wrap.Children.Add(ret) |> ignore
        )
    
let newScope() =
    let values = new Dictionary<string, obj>()
    values.["db"] <- (StatusDb.statusesDb :> obj)
    values.["limits"] <- (Twitter.twitterLimits :> obj)
    values.["helper"] <- (Helpers() :> obj)
    createScope values
let scope = ref (newScope())

clearScope.Click.Add(fun _ ->
    scope := newScope()
)
runCommand.Click.Add(fun _ -> 
    let text = ref ""
    WpfUtils.dispatchMessage commandText (fun _ -> text := commandText.Text)
    log Info (sprintf "Running script %s" (!text))
    
    async { 
        try 
            setAppState "working ... "
            engine.Execute((!text), (!scope)) |> ignore 
            setAppState "done"
        with ex -> 
            printfn "Unable to execute script  %s" (!text)
            printfn "%A" ex 
            setAppState "error"
    } |> Async.Start
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