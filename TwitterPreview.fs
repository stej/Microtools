module program

open System
open System.Xml
open Utils
open OAuth
open Status
open System.Windows.Threading

OAuth.checkAccessTokenFile()

(**************************************)
(* wrapPanel and scroll: http://social.msdn.microsoft.com/forums/en-US/wpf/thread/02cf717c-1191-4266-b850-91b8a2716ba6 *)
open System.Windows
open System.Windows.Controls
open System.Windows.Data

let firstStatusId : Int64 = Int64.MinValue

let window = WpfUtils.createXamlWindow "TwitterPreview.xaml"
let switcher = window.FindName("switchView") :?> Button
let up = window.FindName("up") :?> Button
let wrap = window.FindName("images") :?> WrapPanel
let imagesHolder = window.FindName("imagesHolder") :?> UIElement
let detailsHolder = window.FindName("detailsHolder") :?> UIElement
let details = window.FindName("statusDetails") :?> StackPanel
let limitCtl = window.FindName("limit") :?> TextBlock
let appStateCtl = window.FindName("appState") :?> TextBlock

let fillPictures statuses =
    wrap.Children.Clear()
    statuses 
      |> Seq.map (fun status -> WpfUtils.createLittlePicture status) 
      |> Seq.iter (fun pic -> wrap.Children.Add(pic) |> ignore)
let fillDetails statuses =
    details.Children.Clear()
    statuses 
      |> Seq.map (fun status -> (fst (WpfUtils.createDetail status))) 
      |> Seq.iter (fun statusctl -> details.Children.Add(statusctl) |> ignore)

let setAppState state = 
    WpfUtils.dispatchMessage appStateCtl (fun _ -> appStateCtl.Text <- state)

Twitter.twitterLimits.Start()

window.Loaded.Add(
    fun _ ->
        async {
          let rec asyncloop() = 
            setAppState "Loading.."
            StatusesReplies.loadNewPersonalStatuses()    // or StatusesReplies.loadPublicStatuses
                |> ImagesSource.ensureStatusesImages
                |> PreviewsState.userStatusesState.AddStatuses
            WpfUtils.dispatchMessage wrap (fun _ -> let list,tree = PreviewsState.userStatusesState.GetStatuses()
                                                    fillPictures list
                                                    fillDetails tree)
            setAppState "Done.."
            async { do! Async.Sleep(1000*60*5) } |> Async.RunSynchronously
            asyncloop()
          asyncloop()
        } |> Async.Start
        
        async {
            let rec asyncLoop() =
                let limits = Twitter.twitterLimits.GetLimitsString()
                printfn "limits: %s" limits
                WpfUtils.dispatchMessage limitCtl (fun r -> limitCtl.Text <- limits)
                async { do! Async.Sleep(2500) } |> Async.RunSynchronously
                asyncLoop()
            asyncLoop()
        } |> Async.Start
)
up.Click.Add(fun _ -> 
    async {
        let firstStatusId:Int64 = 
            match PreviewsState.userStatusesState.GetFirstStatusId() with
            | None     -> printf "unknown first status"
                          Int64.MaxValue
            | Some(id) -> printf "first status id is %d" id
                          id
        StatusDb.statusesDb.GetTimelineStatusesBefore(50,firstStatusId)
            |> Seq.toList
            |> ImagesSource.ensureStatusesImages
            |> PreviewsState.userStatusesState.AddStatuses
        let list,tree =  PreviewsState.userStatusesState.GetStatuses()
        WpfUtils.dispatchMessage wrap (fun _ -> fillPictures list; fillDetails tree)
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

[<System.STAThread>]
(new Application()).Run(window) |> ignore