module program

open System
open System.Xml
open Utils
open OAuth
open Status
open System.Windows.Threading
open System.Linq
open DbFunctions
open TwitterLimits

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
let setAppState1 format p1 = WpfUtils.dispatchMessage appStateCtl (fun _ -> appStateCtl.Text <- String.Format(format, [|p1|]))
let setAppState2 (format:string) p1 p2 = WpfUtils.dispatchMessage appStateCtl (fun _ -> appStateCtl.Text <- String.Format(format, p1, p2))
let setAppStateCount count = setAppState (sprintf "Done.. Count: %d" count)

let mutable showHiddenStatuses = false
filterCtl.Text <- StatusFilter.defaultConfigFilter
DbFunctions.dbAccess <- StatusDb.statusesDb

// status downloaded from Twitter
Twitter.NewStatusDownloaded 
    |> Event.add (fun sInfo -> dbAccess.SaveStatus(sInfo)
                               setAppState1 "Saving status {0}" sInfo)

twitterLimits.Start()

let fillDetails statuses = DisplayStatus.fillDetails window details filterCtl.Text showHiddenStatuses statuses
let fillPictures = DisplayStatus.fillPictures wrap

let switchPanes () =
    if imagesHolder.Visibility = Visibility.Visible then
      imagesHolder.Visibility <- Visibility.Collapsed
      detailsHolder.Visibility <- Visibility.Visible
    else
      imagesHolder.Visibility <- Visibility.Visible
      detailsHolder.Visibility <- Visibility.Collapsed
      
let refresh () =
    let list,tree = PreviewsState.userStatusesState.GetStatuses()
    WpfUtils.dispatchMessage wrap (fun _ -> fillPictures list
                                            fillDetails tree)
    list,tree

window.Loaded.Add(
    fun _ ->
        async {
          let rec asyncloop() = 
            setAppState "Loading.."
            Twitter.loadNewPersonalStatuses twitterLimits.IsSafeToQueryTwitterStatuses (Twitter.getLastStoredIds())    // or StatusesReplies.loadPublicStatuses
                |> Twitter.saveDownloadedStatuses
                |> fun downloaded -> downloaded.NewStatuses
                |> PreviewsState.userStatusesState.AddStatuses
            let list,trees = PreviewsState.userStatusesState.GetStatuses()
            
            ImagesSource.ensureStatusesImages trees |> ignore
            
            WpfUtils.dispatchMessage wrap (fun _ -> fillPictures list
                                                    fillDetails trees)
            setAppStateCount list.Length
            async { do! Async.Sleep(1000*60*3) } |> Async.RunSynchronously
            asyncloop()
          asyncloop()
        } |> Async.Start
        
        async {
            let rec asyncLoop() =
                let limits = twitterLimits.GetLimitsString()
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
            |> ImagesSource.ensureStatusesImages
            |> PreviewsState.userStatusesState.AddStatuses
        let list,_ = refresh() 
        setAppStateCount list.Length
    } |> Async.Start
)
clear.Click.Add( fun _ ->
    PreviewsState.userStatusesState.ClearStatuses()
    WpfUtils.dispatchMessage wrap (fun _ -> fillPictures []
                                            fillDetails [])
    setAppStateCount 0
)

switcher.Click.Add(fun _ -> switchPanes () )
window.MouseDoubleClick.Add(fun _ -> switchPanes () )

let negateShowHide (menuItem:MenuItem) =
    showHiddenStatuses <- not showHiddenStatuses
    menuItem.Header <- if showHiddenStatuses then "Hide filtered" else "Show filtered"

// bind context menu
do
    window.ContextMenu <- new ContextMenu()
     
    let menuItem = new MenuItem()
    //menuItem.Width = 50
    menuItem.Header <- "Show filtered"
    menuItem.Click.Add(fun _ -> negateShowHide menuItem
                                refresh () |> ignore)
    window.ContextMenu.Items.Add(menuItem) |> ignore

[<assembly: System.Reflection.AssemblyTitle("TwitterClient")>]
[<assembly: System.Runtime.InteropServices.Guid("b607f47b-df94-4c4c-a7ff-1a182bf8d8bb3")>]
()

[<System.STAThread>]
do
    let ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
    linfo (sprintf "%i-%i-%i-%i" ver.Major ver.Minor ver.Build ver.Revision)
    //Updates.update()
    (new Application()).Run(window) |> ignore