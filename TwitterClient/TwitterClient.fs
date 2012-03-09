module program

open System
open System.Xml
open Utils
open OAuth
open Status
open UIModel
open System.Windows.Threading
open System.Linq
open TwitterLimits
open DisplayStatus
open StatusXmlProcessors
open SubscriptionsConfig
open System.Threading
open UIRefresher

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
let findPanel = window.FindName("findPanel") :?> DockPanel
let find = window.FindName("find") :?> TextBox
let clearOnFind = window.FindName("clearOnFind") :?> CheckBox

let runOnUIThread fn =
    WpfUtils.dispatchMessage window fn
let focusTweets() =
    if imagesHolder.Visibility = Visibility.Visible then scrollImages.Focus() |> ignore
    else scrollDetails.Focus() |> ignore
let focusFilter() =
    filterCtl.Focus() |> ignore
let setAppState = UIRefresher.setAppState window appStateCtl
let setAppState1 format p1 = 
    String.Format(format, [|p1|]) |> setAppState

let settings = new ClientSettings.MySettings()
let mutable showOnlyLinkPart = true
let mutable lastRefresh = DateTime.MinValue
let mutable inFindMode = false
filterCtl.Text <- settings.LastFilter
DbInterface.dbAccess <- StatusDb.statusesDb
MediaDbInterface.urlsAccess <- MediaDb.urlsDb
ExtraProcessors.Processors <- [ExtraProcessors.Url.ParseShortUrlsAndStore; ExtraProcessors.Photo.ParseShortUrlsAndStore]
WpfUtils.urlResolver <- new UrlResolver.UrlResolver(MediaDbInterface.urlsAccess)

twitterLimits.Start()

// status downloaded from Twitter
Twitter.NewStatusDownloaded 
    |> Event.add (fun sInfo -> DbInterface.dbAccess.SaveStatus(sInfo)
                               setAppState1 "Saving status {0}" sInfo)

let getCurrentUISettings() = 
    let filterText = ref ""
    runOnUIThread (fun _ -> filterText := filterCtl.Text)
    let color, statusFilterer = 
        match StatusFilter.getStatusFilterer !filterText with
        | Some(parsedExpressions) ->
             Brushes.White, parsedExpressions
        | None ->
            lerr "Parsing filters"
            Brushes.Salmon, fun _ -> false
    runOnUIThread (fun _ -> filterCtl.Background <- color)
    { ShowOnlyLinkPart = showOnlyLinkPart
      Filter = { ShowHidden = settings.ShowFilteredItems; FilterOutRule = statusFilterer } }

let setPanelsVisibility () =
    imagesHolder.Visibility <-  if settings.PreviewPanelVisible then Visibility.Visible else Visibility.Collapsed
    detailsHolder.Visibility <- if settings.PreviewPanelVisible then Visibility.Collapsed else Visibility.Visible
let switchPanels () =
    settings.PreviewPanelVisible <- not settings.PreviewPanelVisible
let setWindowProperties () =
    window.Topmost <- settings.OnTop
    window.Top <- settings.WindowTop
    window.Left <- settings.WindowLeft
    window.Width <- settings.WindowWidth
    window.Height <- settings.WindowHeight
let showHideFindPanel () =
    if inFindMode then
        if find.IsFocused then
            findPanel.Visibility <- Visibility.Collapsed
            inFindMode <- not inFindMode
            focusTweets()
        else
            // the only situation when only focus is changed
            find.Focus() |> ignore
    else
        inFindMode <- not inFindMode
        findPanel.Visibility <- Visibility.Visible
        find.Focus() |> ignore

let refreshWithModel, refresh = 
    let agent = RefresherAgent(window, wrap, details, appStateCtl)
    (fun asyncModelGet -> agent.Refresh(getCurrentUISettings(), asyncModelGet)),
    (fun () -> agent.Refresh(getCurrentUISettings()))

window.Loaded.Add(fun _ ->
    setAppState "Loading.."
    let rec asyncloop checkerfce statusesType = 
        async { 
            while inFindMode do
                do! Async.Sleep(1000)   // sleep second, don't update in edit mode

            let getstatuses = async {
                            let! statuses = checkerfce() 
                            statuses |> Twitter.PersonalStatuses.saveStatuses statusesType
                            match statuses with
                            | Some(l) when l.Length > 0 -> PreviewsState.userStatusesState.AddStatuses l
                                                           return true
                            | _                         -> return false
            }
            let! refreshWindow = getstatuses
            if refreshWindow then
                refresh ()
            
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
            runOnUIThread (fun r -> limitCtl.Text <- limits)
            async { do! Async.Sleep(2500) } |> Async.RunSynchronously
            asyncLoop()
        asyncLoop()
    } |> Async.Start
)

filterCtl.KeyDown.Add(fun args ->
    if args.Key = Key.Enter then
        settings.LastFilter <- filterCtl.Text
        refresh()
)
find.KeyDown.Add(fun args -> 
    if args.Key = Key.Enter then
        // start async, because this handler runs on UI thread.. the app would not be responsive
        // and furthermore some UI state is reported during adding statuses which caused deadlocks..
        let textToFind = find.Text
        let clear = clearOnFind.IsChecked.HasValue && clearOnFind.IsChecked.Value
        async { 
            let! found = TweetsFinder.find textToFind 
            if clear then 
                PreviewsState.userStatusesState.ClearStatuses ()
            PreviewsState.userStatusesState.AddStatusesWithoutDownload found

            runOnUIThread (fun _ -> focusTweets())
        } |> refreshWithModel
)

let goUp () = 
    async {
        let firstStatusId:Int64 = 
            match PreviewsState.userStatusesState.GetFirstStatusId() with
            | None     -> linfo "unknown first status"
                          Int64.MaxValue
            | Some(id) -> linfop "first status id is {0}" id
                          id
        let! statuses = StatusDb.statusesDb.AsyncGetTimelineStatusesBefore(Utils.Settings.UpCount,firstStatusId)
        statuses |> PreviewsState.userStatusesState.AddStatuses
        
    } |> refreshWithModel

let negateShowHide (menuItem:MenuItem) ()=
    settings.ShowFilteredItems <- not settings.ShowFilteredItems
    menuItem.IsChecked <- settings.ShowFilteredItems

// bind context menu
do
    let addMenu menu =
        window.ContextMenu.Items.Add(menu) |> ignore    
        menu

    window.ContextMenu <- new ContextMenu()
     
    let menuShowFiltered = new MenuItem(Header = "Show f_iltered", 
                                        IsCheckable = true, 
                                        ToolTip = "Show/hide filtered items")               |> addMenu
    let menuSwitch = new MenuItem(Header = "Switc_h", ToolTip = "Switch to list/tree view") |> addMenu
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
    menuTop.IsChecked <- settings.OnTop
    menuShowFiltered.IsChecked <- settings.ShowFilteredItems

    let negateTopmost (menuItem:MenuItem) () = 
        settings.OnTop <- not settings.OnTop
        window.Topmost <- settings.OnTop
        menuItem.IsChecked <- settings.OnTop
    WpfUtils.Commands.bindCommand Key.E ModifierKeys.Control (switchPanels>>setPanelsVisibility>>focusTweets) window menuSwitch
    WpfUtils.Commands.bindCommand Key.I ModifierKeys.Control (negateShowHide menuShowFiltered>>focusTweets>>refresh) window menuShowFiltered
    WpfUtils.Commands.bindCommand Key.C ModifierKeys.Control (PreviewsState.userStatusesState.ClearStatuses>>focusTweets>>refresh) window menuClear
    WpfUtils.Commands.bindCommand Key.U ModifierKeys.Control (goUp>>focusTweets) window menuUp
    WpfUtils.Commands.bindCommand Key.T ModifierKeys.Control (negateTopmost menuTop>>focusTweets) window menuTop
    WpfUtils.Commands.bindClick MouseAction.LeftDoubleClick (switchPanels>>setPanelsVisibility>>focusTweets) window null
    WpfUtils.Commands.bindCommand Key.Add ModifierKeys.Control (WpfUtils.UISize.zoomIn>>focusTweets>>refresh) window null
    WpfUtils.Commands.bindCommand Key.Subtract ModifierKeys.Control (WpfUtils.UISize.zoomOut>>focusTweets>>refresh) window null
    WpfUtils.Commands.bindCommand Key.F ModifierKeys.Control (showHideFindPanel) window null
    WpfUtils.Commands.bindCommand Key.I (ModifierKeys.Control|||ModifierKeys.Shift) focusFilter window null

    setPanelsVisibility ()
    setWindowProperties ()

[<assembly: System.Reflection.AssemblyTitle("TwitterClient")>]
[<assembly: System.Runtime.InteropServices.Guid("b607f47b-df94-4c4c-a7ff-1a182bf8d8bb3")>]
()

[<System.STAThread>]
do
    let ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
    linfo (sprintf "%i-%i-%i-%i" ver.Major ver.Minor ver.Build ver.Revision)
    //Updates.update()
    (new Application()).Run(window) |> ignore
    settings.WindowTop <- window.Top
    settings.WindowLeft <- window.Left
    settings.WindowWidth <- window.Width
    settings.WindowHeight <- window.Height
    settings.Save()