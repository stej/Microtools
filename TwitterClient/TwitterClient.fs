module program

open System
open System.Xml
open Utils
open OAuth
open Status
open System.Windows.Threading
open System.Linq

OAuth.checkAccessTokenFile()

(**************************************)
(* wrapPanel and scroll: http://social.msdn.microsoft.com/forums/en-US/wpf/thread/02cf717c-1191-4266-b850-91b8a2716ba6 *)
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media

let window = WpfUtils.createXamlWindow "TwitterClient.xaml"
let switcher = window.FindName("switchView") :?> Button
let up = window.FindName("up") :?> Button
let clear = window.FindName("clear") :?> Button
let wrap = window.FindName("images") :?> WrapPanel
let imagesHolder = window.FindName("imagesHolder") :?> UIElement
let detailsHolder = window.FindName("detailsHolder") :?> UIElement
let details = window.FindName("statusDetails") :?> StackPanel
let limitCtl = window.FindName("limit") :?> TextBlock
let appStateCtl = window.FindName("appState") :?> TextBlock
let filterCtl = window.FindName("filter") :?> TextBox

let setAppState state = WpfUtils.dispatchMessage appStateCtl (fun _ -> appStateCtl.Text <- state)
let setAppState1 format p1 = WpfUtils.dispatchMessage appStateCtl (fun _ -> appStateCtl.Text <- String.Format(format, p1))
let setAppState2 (format:string) p1 p2 = WpfUtils.dispatchMessage appStateCtl (fun _ -> appStateCtl.Text <- String.Format(format, p1, p2))

filterCtl.Text <- Utils.Settings.Filter

// status downloaded from Twitter
Twitter.NewStatusDownloaded 
    |> Event.add (fun (source,status) -> StatusDb.statusesDb.SaveStatus(source, status)
                                         setAppState2 "Saving status {0} - {1}" status.UserName status.StatusId)

let fillPictures statuses =
    wrap.Children.Clear()
    statuses 
      |> Seq.sortBy (fun status -> status.StatusId)
      |> Seq.map (fun status -> WpfUtils.createLittlePicture status) 
      |> Seq.iter (fun pic -> wrap.Children.Add(pic) |> ignore)
let fillDetails statuses =
    let filter = parseFilter filterCtl.Text
    let showStatus rootStatus =
        let controls = WpfUtils.createConversationControls WpfUtils.End details
        WpfUtils.setNewConversation controls rootStatus
        |> Seq.iter (fun detailCtl ->   //conversationNodeControlsInfo
                        if detailCtl.Status.StatusId < PreviewsState.userStatusesState.GetFirstStatusId().Value then
                            detailCtl.Detail.Opacity <- 0.4
                        if detailCtl.Status.MatchesFilter(filter) then
                            detailCtl.Detail.Opacity <- 0.2
                     )
    details.Children.Clear()
    statuses 
      |> Seq.sortBy (fun status -> status |> Status.GetStatusIdsForNode |> Seq.sortBy (fun statusid -> -statusid) |> Seq.nth 0)
      |> Seq.iter (fun rootStatus -> WpfUtils.dispatchMessage window (fun f -> showStatus rootStatus))

Twitter.twitterLimits.Start()

window.Loaded.Add(
    fun _ ->
        async {
          let rec asyncloop() = 
            setAppState "Loading.."
            Twitter.loadNewPersonalStatuses()    // or StatusesReplies.loadPublicStatuses
                |> PreviewsState.userStatusesState.AddStatuses
            let list,tree = PreviewsState.userStatusesState.GetStatuses()
            Flatten tree 
                |> Seq.toList
                |> ImagesSource.ensureStatusesImages
                |> ignore
            WpfUtils.dispatchMessage wrap (fun _ -> fillPictures list
                                                    fillDetails tree)
            setAppState (sprintf "Done.. Count: %d" list.Length)
            async { do! Async.Sleep(1000*60*5) } |> Async.RunSynchronously
            asyncloop()
          asyncloop()
        } |> Async.Start
        
        async {
            let rec asyncLoop() =
                let limits = Twitter.twitterLimits.GetLimitsString()
                ldbgp "limits: {0}" limits
                WpfUtils.dispatchMessage limitCtl (fun r -> limitCtl.Text <- limits)
                async { do! Async.Sleep(2500) } |> Async.RunSynchronously
                asyncLoop()
            asyncLoop()
        } |> Async.Start
)

// react on changes in filter; what is this cast? http://stackoverflow.com/questions/5131372/how-to-convert-a-wpf-button-click-event-into-observable-using-rx-and-f
(filterCtl.TextChanged :> IObservable<_>)
    .Throttle(TimeSpan.FromMilliseconds(800.))
    .DistinctUntilChanged()
    .Subscribe(fun _ -> 
        let list,tree = PreviewsState.userStatusesState.GetStatuses()
        WpfUtils.dispatchMessage wrap (fun _ -> fillPictures list
                                                fillDetails tree)
    ) |> ignore

up.Click.Add(fun _ -> 
    async {
        let firstStatusId:Int64 = 
            match PreviewsState.userStatusesState.GetFirstStatusId() with
            | None     -> linfo "unknown first status"
                          Int64.MaxValue
            | Some(id) -> linfop "first status id is {0}" id
                          id
        StatusDb.statusesDb.GetTimelineStatusesBefore(50,firstStatusId)
            |> Seq.toList
            |> ImagesSource.ensureStatusesImages
            |> PreviewsState.userStatusesState.AddStatuses
        let list,tree =  PreviewsState.userStatusesState.GetStatuses()
        WpfUtils.dispatchMessage wrap (fun _ -> fillPictures list; fillDetails tree)
    } |> Async.Start
)
clear.Click.Add( fun _ ->
    PreviewsState.userStatusesState.ClearStatuses()
    WpfUtils.dispatchMessage wrap (fun _ -> fillPictures []; fillDetails [])
)

switcher.Click.Add(fun _ ->
  if imagesHolder.Visibility = Visibility.Visible then
    imagesHolder.Visibility <- Visibility.Collapsed
    detailsHolder.Visibility <- Visibility.Visible
  else
    imagesHolder.Visibility <- Visibility.Visible
    detailsHolder.Visibility <- Visibility.Collapsed
)
[<assembly: System.Reflection.AssemblyTitle("TwitterClient")>]
[<assembly: System.Runtime.InteropServices.Guid("b607f47b-df94-4c4c-a7ff-1a182bf8d8bb3")>]
()

[<System.STAThread>]
do
    let ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
    linfo (sprintf "%i-%i-%i-%i" ver.Major ver.Minor ver.Build ver.Revision)
    //Updates.update()
    (new Application()).Run(window) |> ignore