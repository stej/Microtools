module program

open System
open System.Xml
open Utils
open OAuth
open Status
open System.Windows.Threading
open System.Linq
open TwitterLimits
open DisplayStatus
open StatusXmlProcessors
open SubscriptionsConfig
open System.Threading

OAuth.checkAccessTokenFile()

(**************************************)
(* wrapPanel and scroll: http://social.msdn.microsoft.com/forums/en-US/wpf/thread/02cf717c-1191-4266-b850-91b8a2716ba6 *)
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media
open System.Windows.Input

let window = WpfUtils.createXamlWindow "TwitterClient.xaml"
let wrap = window.FindName("images") :?> WrapPanel
let imagesHolder = window.FindName("imagesHolder") :?> UIElement
let detailsHolder = window.FindName("detailsHolder") :?> UIElement
let details = window.FindName("statusDetails") :?> StackPanel
let limitCtl = window.FindName("limit") :?> TextBlock
let appStateCtl = window.FindName("appState") :?> TextBlock
let filterCtl = window.FindName("filter") :?> TextBox
let scrollDetails = window.FindName("scrollDetails") :?> ScrollViewer
let scrollImages = window.FindName("scrollImages") :?> ScrollViewer
//let tweetsWrapper = window.FindName("tweetsWrapper") :?> Panel
//let contentGrid = window.FindName("content") :?> Panel

let setAppState state = 
    WpfUtils.dispatchMessage appStateCtl (fun _ -> appStateCtl.Text <- state)
let setAppState1 format p1 = 
    String.Format(format, [|p1|]) |> setAppState
let setAppState2 (format:string) p1 p2 = 
    String.Format(format, p1, p2) |> setAppState
let setAppStateCount () = 
    setAppState (UIState.getAppStrState())
let setCount count (filterStatusInfos: WpfUtils.StatusInfoToDisplay list) = 
    let filtered = filterStatusInfos |> List.fold (fun count curr -> if curr.FilterInfo.Filtered then count+1 else count) 0
    UIState.setCounts count filtered
let focusTweets() =
    if imagesHolder.Visibility = Visibility.Visible then scrollImages.Focus() |> ignore
    else scrollDetails.Focus() |> ignore

let mutable showHiddenStatuses = false
let mutable showOnlyLinkPart = true
let mutable lastRefresh = DateTime.MinValue
filterCtl.Text <- "#f:"+StatusFilter.defaultConfigFilter
DbInterface.dbAccess <- StatusDb.statusesDb
ShortenerDbInterface.urlsAccess <- UrlDb.urlsDb
ExtraProcessors.Processors <- [ExtraProcessors.Url.ParseShortUrlsAndStore]
WpfUtils.urlResolver <- new UrlResolver.UrlResolver(ShortenerDbInterface.urlsAccess)

let getCurrentUISettings() = 
    let filterText = ref ""
    WpfUtils.dispatchMessage wrap (fun _ -> filterText := filterCtl.Text)
    let color, statusFilterer = 
        match StatusFilter.getStatusFilterer !filterText with
        | Some(parsedExpressions) ->
             Brushes.White, parsedExpressions
        | None ->
            lerr "Parsing filters"
            Brushes.Salmon, fun _ -> false
    WpfUtils.dispatchMessage wrap (fun _ -> filterCtl.Background <- color)
    { ShowOnlyLinkPart = showOnlyLinkPart
      Filter = { ShowHidden = showHiddenStatuses; FilterOutRule = statusFilterer } }

// status downloaded from Twitter
Twitter.NewStatusDownloaded 
    |> Event.add (fun sInfo -> DbInterface.dbAccess.SaveStatus(sInfo)
                               setAppState1 "Saving status {0}" sInfo)

twitterLimits.Start()

let fillDetails filter  = DisplayStatus.FilterAwareConversation.fill details filter
let fillPictures filter =  DisplayStatus.LitlePreview.fill wrap filter

let switchPanes () =
    if imagesHolder.Visibility = Visibility.Visible then
      imagesHolder.Visibility <- Visibility.Collapsed
      detailsHolder.Visibility <- Visibility.Visible
    else
      imagesHolder.Visibility <- Visibility.Visible
      detailsHolder.Visibility <- Visibility.Collapsed
   
let controlsCache = new System.Collections.Concurrent.ConcurrentDictionary<Int64, WpfUtils.conversationNodeControlsInfo>()
let fillCache items = 
    let addToCache (item:WpfUtils.conversationNodeControlsInfo) = 
        let id = item.StatusToDisplay.StatusInfo.LogicalStatusId()
        controlsCache.[id] <- item
    controlsCache.Clear()
    ldbg "CURLS: Clearing ctls cache"
    items |> List.map (fun (mainCtls, statusesCtls) -> statusesCtls)
          |> List.concat
          |> List.iter addToCache
    lastRefresh <- DateTime.Now
    ldbgp2 "CURLS: update last refresh to {0}, cache size: {1}" lastRefresh controlsCache.Count

let resolveUrls () = 
    async {
        let started = DateTime.Now
        linfop2 "CURLS: Starting resolving urls from {0}, thread id: {1}" started Thread.CurrentThread.ManagedThreadId
        let expandControlUrls id =
            async { 
                match controlsCache.TryGetValue(id) with
                    | true, v -> 
                           do! v.StatusToDisplay.ExpandUrls()
                           let currentUISettings = getCurrentUISettings ()
                           WpfUtils.dispatchMessage wrap (fun _ -> FilterAwareConversation.updateText currentUISettings  v)
                    | _  -> ()
            }
        let ids = controlsCache.Keys |> Seq.sort |> Seq.toList
        linfop2 "CURLS: Count of ids to resolve: {0}, thread id {1}" ids.Length Thread.CurrentThread.ManagedThreadId 

        for id in ids do
            if started >= lastRefresh then
                do! expandControlUrls id
            
        linfop2 "CURLS: resolving urls ended from {0}, thread id: {1}" started Thread.CurrentThread.ManagedThreadId

    } |> Async.Start

let refresh =
    let refresher = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop () = async {
                let! msg = mbox.Receive()
                let list,trees = PreviewsState.userStatusesState.GetStatuses()
                ldbgp2 "CLI: Count of statuses: {0}/{1}" list.Length trees.Length

                ImagesSource.ensureStatusesImages trees |> ignore
                ldbg "CLI: Refreshing panels"

                let uiSettings = getCurrentUISettings()
                
                let filterStatusInfos = fillPictures uiSettings list
                setCount list.Length filterStatusInfos
                setAppStateCount ()

                let detailsCtls = fillDetails uiSettings trees
                fillCache detailsCtls

                resolveUrls ()
                ldbg "CLI: Refresh done"
                return! loop()
            }
            loop ())
    fun () -> refresher.Post("")



let StatusesLoadedEvent = new Event<statusInfo list option>()
let StatusesLoadedPublished = StatusesLoadedEvent.Publish

// event is not neede actually, maybe convert back..
StatusesLoadedPublished |> Event.add (fun list ->
    UIState.addDone() 
    match list with
    | Some(l) when l.Length > 0 -> l |> PreviewsState.userStatusesState.AddStatuses
                                   refresh()
    | _                         -> ()
)
window.Loaded.Add(fun _ ->
    setAppState "Loading.."
    let rec asyncloop checkerfce statusesType = 
        async { 
            UIState.addWorking()
            setAppStateCount ()

            let! statuses = checkerfce() 
            statuses |> Twitter.PersonalStatuses.saveStatuses statusesType
            statuses |> StatusesLoadedEvent.Trigger 

            setAppStateCount ()    // at least show that we are done..
            do! Async.Sleep(Utils.Settings.TwitterClientFetchInterval*1000)
            return! asyncloop checkerfce statusesType
        }

    GetTwitterCheckersFromSubscriptions ()
    |> List.iter (fun (checkerfce, statusesType) -> asyncloop checkerfce statusesType |> Async.Start)
    // retweets are handled by FriendsStatuses; leaving here for debugging and for cases that Friends don't work because of a bug...
    //asyncloop Twitter.PersonalStatuses.retweetsChecker.Check Twitter.RetweetsStatuses |> Async.Start
        
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
    .Subscribe(fun _ -> refresh()) |> ignore

let goUp () = 
    async {
        let firstStatusId:Int64 = 
            match PreviewsState.userStatusesState.GetFirstStatusId() with
            | None     -> linfo "unknown first status"
                          Int64.MaxValue
            | Some(id) -> linfop "first status id is {0}" id
                          id
        StatusDb.statusesDb.GetTimelineStatusesBefore(Utils.Settings.UpCount,firstStatusId)
            |> ImagesSource.ensureStatusesImages
            |> PreviewsState.userStatusesState.AddStatuses
        refresh() 
    } |> Async.Start

//contentGrid.MouseDoubleClick.Add(fun _ -> switchPanes () )
//contentGrid.PreviewDoubleClick.Add(fun _ -> switchPanes () )
//imagesHolder.MouseRightButtonUp.Add(fun _ -> switchPanes () )
//detailsHolder.MouseRightButtonUp.Add(fun _ -> switchPanes () )

//http://stackoverflow.com/questions/5228364/reactive-framework-doubleclick
//http://stackoverflow.com/questions/1274378/cleanest-single-click-double-click-handling-in-silverlight
// nefunguje
//(contentGrid.PreviewMouseDown :> IObservable<_>)
//   .TimeInterval()
//   .Subscribe(fun (evt:TimeInterval<Input.MouseButtonEventArgs>) -> if evt.Interval.TotalMilliseconds < 300. then switchPanes ())
//   |> ignore
//(window.PreviewMouseDown :> IObservable<_>)
//   .TimeInterval()
//   .Subscribe(fun (evt:TimeInterval<Input.MouseButtonEventArgs>) -> if evt.Interval.TotalMilliseconds < 300. then switchPanes ())
//   |> ignore
//(imagesHolder.MouseLeftButtonDown :> IObservable<_>).Merge(
//(detailsHolder.MouseLeftButtonDown :> IObservable<_>))
//   .TimeInterval()
//   .Subscribe(
//      fun (evt:TimeInterval<Input.MouseButtonEventArgs>) -> 
//         if evt.Interval.TotalMilliseconds < 300. then switchPanes ()
//   )
//   |> ignore

let negateShowHide (menuItem:MenuItem) ()=
    showHiddenStatuses <- not showHiddenStatuses
    menuItem.IsChecked <- showHiddenStatuses

// bind context menu
do
    let addMenu menu =
        window.ContextMenu.Items.Add(menu) |> ignore    
        menu

    window.ContextMenu <- new ContextMenu()
     
    let menuShowFiltered = new MenuItem(Header = "Show _filtered", 
                                        IsCheckable = true, 
                                        ToolTip = "Show/hide filtered items")               |> addMenu
    let menuSwitch = new MenuItem(Header = "_Switch", ToolTip = "Switch to list/tree view") |> addMenu
    let menuClear = new MenuItem(Header = "_Clear", ToolTip = "Clear view")                 |> addMenu
    let menuUp = new MenuItem(Header = "Go _up", ToolTip = "Get older statuses")            |> addMenu
    let menuShortLinks = new MenuItem(Header = "Short links", 
                                      ToolTip = "Show only part of the link",
                                      IsCheckable = true)                                   |> addMenu
    let menuTop = new MenuItem(Header = "On top", 
                                      ToolTip = "Is on top of other windows",
                                      IsCheckable = true)                                   |> addMenu
    menuShortLinks.IsChecked <- showOnlyLinkPart
    menuShortLinks.Click.Add(fun _ -> showOnlyLinkPart <- not showOnlyLinkPart
                                      refresh ())
    menuTop.IsChecked <- window.Topmost

    let negateTopmost (menuItem:MenuItem) () = 
        window.Topmost <- not window.Topmost
        menuItem.IsChecked <- window.Topmost
    WpfUtils.Commands.bindCommand Key.S (switchPanes>>focusTweets) window menuSwitch
    WpfUtils.Commands.bindCommand Key.F (negateShowHide menuShowFiltered>>refresh>>focusTweets) window menuShowFiltered
    WpfUtils.Commands.bindCommand Key.C (PreviewsState.userStatusesState.ClearStatuses>>refresh>>focusTweets) window menuClear
    WpfUtils.Commands.bindCommand Key.U (goUp>>focusTweets) window menuUp
    WpfUtils.Commands.bindCommand Key.T (negateTopmost menuTop>>focusTweets) window menuTop
    WpfUtils.Commands.bindClick MouseAction.LeftDoubleClick (switchPanes>>focusTweets) window null

[<assembly: System.Reflection.AssemblyTitle("TwitterClient")>]
[<assembly: System.Runtime.InteropServices.Guid("b607f47b-df94-4c4c-a7ff-1a182bf8d8bb3")>]
()

[<System.STAThread>]
do
    let ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
    linfo (sprintf "%i-%i-%i-%i" ver.Major ver.Minor ver.Build ver.Revision)
    //Updates.update()
    (new Application()).Run(window) |> ignore