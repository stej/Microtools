module program

open System
open System.Xml
open Utils
open OAuth
open Status
open System.Windows.Threading
open System.Threading

let args = System.Environment.GetCommandLineArgs()
let statusId = match args with
                | [|_; sid |] -> Int64.Parse(sid)
                //| _ -> failwith "No argument specified. Specify status id to process"
                | _ -> -1L
//let statusId = 15703340935544832L
//let statusId = 16145133250547712L

OAuth.checkAccessTokenFile()

(**************************************)
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media
open System.Configuration

let panelWorkingBrush = new SolidColorBrush(Color.FromRgb(255uy, 200uy, 200uy))
let panelWaitingBrush = new SolidColorBrush(Color.FromRgb(200uy, 200uy, 200uy))

let window = WpfUtils.createXamlWindow "TwitterConversation.xaml"
let limitCtl = window.FindName("limit") :?> TextBlock
let panel = window.FindName("conversations") :?> StackPanel
let appStateCtl = window.FindName("appState") :?> TextBlock
let updateAll = window.FindName("updateAll") :?> Button
let pauseUpdate = window.FindName("pause") :?> Button
let continueUpdate = window.FindName("continue") :?> Button
let cancelUpdate = window.FindName("cancel") :?> Button

let mutable (lastUpdateall:DateTime) = DateTime.MinValue

let setState text = 
    WpfUtils.dispatchMessage appStateCtl (fun _ -> appStateCtl.Text <- text)
    printfn "%s" text
   
let updateTwitterLimit = 
    async {
        let rec asyncLoop() =
            let limits = Twitter.twitterLimits.GetLimitsString()
            printfn "limits: %s" limits
            WpfUtils.dispatchMessage limitCtl (fun r -> limitCtl.Text <- limits)
            async { do! Async.Sleep(5000) } |> Async.RunSynchronously
            asyncLoop()
        asyncLoop()
    }

let readSingleStatus() =
    match Twitter.getStatus Status.RequestedConversation statusId with
     | Some(s) -> match StatusesReplies.rootConversation s with
                    | Some(r) -> r
                    | None -> printfn "Unable to find conversation root from %d" statusId
                              Environment.Exit(0)
                              failwith ""
     | None -> printfn "Unable to load status with id %d" statusId
               Environment.Exit(0)
               failwith ""
let readStatuses() =
    if statusId = -1L then
        let maxConversations = Int32.Parse(ConfigurationManager.AppSettings.["defaultConversationsCount"])
        StatusDb.statusesDb.GetRootStatusesHavingReplies(maxConversations)
    else
        seq { yield readSingleStatus() }

let private statusUpdated = new Event<WpfUtils.conversationControls * status>()
let StatusUpdated = statusUpdated.Publish

let controlsCache = new System.Collections.Concurrent.ConcurrentDictionary<Int64, WpfUtils.conversationControls>()
    
let (showConversationIsUpdating, showConversationIsReady, showConversationWillBeProcessed) =
    let updateBackground (wrapperCtl:StackPanel) brush = 
        WpfUtils.dispatchMessage wrapperCtl (fun _ -> wrapperCtl.Background <- brush)
    ((fun ctl -> updateBackground ctl panelWorkingBrush), 
     (fun ctl -> updateBackground ctl Brushes.White),
     (fun ctl -> updateBackground ctl panelWaitingBrush))
    
let getAsyncConversationUpdate (controls:WpfUtils.conversationControls) rootStatus =
    let wrapper = controls.Wrapper
    async { 
        showConversationIsUpdating wrapper
        // todo - remove cloneStatus and make status immutable
        let currentStatus = ConversationState.conversationsState.GetConversation rootStatus.StatusId
        currentStatus
            |> cloneStatus
            |> StatusesReplies.findReplies
            |> ConversationState.conversationsState.UpdateConversation 
            |> ignore
        let updatedStatus = ConversationState.conversationsState.GetConversation rootStatus.StatusId
        statusUpdated.Trigger(controls, updatedStatus)
        showConversationIsReady wrapper
    }

let bindUpdate (controls:WpfUtils.conversationControls) status =
    controls.UpdateButton.Click.Add(fun _ -> 
        getAsyncConversationUpdate (controls:WpfUtils.conversationControls) status |> Async.Start
    )
    
(*let bindDelete (controls:WpfUtils.conversationControls) status =
    controls.DeleteButton.Click.Add(fun _ ->
        async { 
            setState (sprintf "Deleting tree for %s - %d" status.UserName status.StatusId)
            let rec del status =
                setState (sprintf "Deleting %s - %d" status.UserName status.StatusId)
                status.Children |> Seq.iter del
                StatusDb.statusesDb.DeleteStatus status
            del status
            ConversationState.conversationsState.RemoveConversation status
            
            WpfUtils.dispatchMessage controls.Statuses (fun _ -> controls.Wrapper.Children.Clear())
            setState (sprintf "Deleting tree for %s - %d finished" status.UserName status.StatusId)
        } |> Async.Start
    )*)

let addConversationCtls addTo rootStatus =
    WpfUtils.dispatchMessage window (fun _ -> let controls = WpfUtils.createConversationControls addTo panel |> WpfUtils.addUpdateButton
                                              controlsCache.[rootStatus.StatusId] <- controls
                                              bindUpdate controls rootStatus
                                              //bindDelete controls rootStatus
                                              )
    rootStatus
    
let freshStatusColorer = (fun status -> status.Inserted >= lastUpdateall), Brushes.Yellow
/// refreshes the status (all the conversation)
/// @fnShouldColor = function that returns true if @color should be applied
let refreshOneConversationEx (colorers:((status->bool)*SolidColorBrush) list) rootStatus =
    let controls = controlsCache.[rootStatus.StatusId]
    WpfUtils.dispatchMessage controls.Statuses (fun _ -> 
        for detailCtl in WpfUtils.updateConversation controls rootStatus do
            let color = colorers |> List.tryPick (fun (fn,color) -> if fn detailCtl.Status then Some(color) else None)
            match color with
            | None -> ()
            | Some(c) -> detailCtl.Detail.Background <- c
    )
let refreshOneConversation rootStatus =
    refreshOneConversationEx [freshStatusColorer] rootStatus

let setNewConversationContent rootStatus =
    let controls = controlsCache.[rootStatus.StatusId]
    WpfUtils.dispatchMessage controls.Statuses (fun _ -> 
        WpfUtils.setNewConversation controls rootStatus |> ignore
    )

StatusUpdated.Add(fun (controls, updatedStatus) ->
    WpfUtils.dispatchMessage controls.Statuses (fun _ ->
        refreshOneConversation updatedStatus
    )
)

Twitter.twitterLimits.Start()

window.Loaded.Add(fun _ ->
    // status added to tree
    StatusesReplies.StatusAdded |> Event.add ImagesSource.ensureStatusImageNoRet
    // show what status is loaded
    StatusesReplies.LoadingStatusReplyTree 
        |> Event.add (fun status -> setState (sprintf "Loading %s - %d" status.UserName status.StatusId))
    // status downloaded from Twitter
    Twitter.NewStatusDownloaded 
        |> Event.add (fun (source,status) -> StatusDb.statusesDb.SaveStatus(source, status)
                                             printf "s")
    // some children loaded
    StatusesReplies.SomeChildrenLoaded 
        |> Event.add (fun rootStatus -> setNewConversationContent rootStatus)

    async {
        setState "Reading.."
        readStatuses()
            |> Seq.map ImagesSource.ensureStatusImage
            |> Seq.iter (fun status -> status |> addConversationCtls WpfUtils.End
                                              |> StatusesReplies.loadSavedReplyTree
                                              |> ConversationState.conversationsState.AddConversation
                                              |> setNewConversationContent
               )
        setState "Done.."
    } |> Async.Start

    updateTwitterLimit |> Async.Start
)

// add statuses, that were not visible, because they hadn't any children, but now, they got new children through 
// all the searches
let addNewlyFoundConversations() =
    readStatuses() 
            |> Seq.filter (fun status -> not (ConversationState.conversationsState.ContainsStatus(status.StatusId)))
            |> Seq.map ImagesSource.ensureStatusImage
            |> Seq.iter (fun status -> status |> addConversationCtls WpfUtils.Beginning
                                                |> StatusesReplies.loadSavedReplyTree
                                                |> ConversationState.conversationsState.AddConversation
                                                |> setNewConversationContent)
let addNewlyFoundStatuses() =
    Utils.log Utils.Info "Looking for newly found statuses"
    let checkConversationForNewChildren root =
        // newly added statuses; global for all the conversation
        let news = new ResizeArray<status>()

        // checks and adds new children for given status to @news list
        let rec checkStatusForNewChildren status =
            StatusesReplies.newlyAddedStatusesState.GetNewReplies (status, status.ChildrenIds())
            |> Seq.map (doAndRet news.Add)
            |> Seq.iter status.Children.Add
            status.Children.Sort(fun s1 s2 -> s1.StatusId.CompareTo(s2.StatusId))
            status.Children
            |> Seq.iter checkStatusForNewChildren
        checkStatusForNewChildren root
        (root, news)

    let statusInNews news status =                                          // returns true if passed status is news list
        news |> Seq.exists (fun child -> child.StatusId = status.StatusId)
    let newlyAddedStatusColorer news = 
        statusInNews news, Brushes.LightSalmon                              // fn that takes one param - news and returns tuple; frst is fn taking status
    ConversationState.conversationsState.GetConversations()
    |> List.map checkConversationForNewChildren
    |> List.filter (fun (root,newstats) -> newstats.Count > 0)
    |> List.map (doAndRet (fun (root,newstats) -> Utils.log Utils.Info (sprintf "%s %s has NEW STATUSES. Count: %d" root.UserName root.Text newstats.Count)))
    |> List.iter (fun (root,newstats) -> refreshOneConversationEx [newlyAddedStatusColorer newstats; freshStatusColorer] root)
    
    
let mutable (cts:CancellationTokenSource) = null
let mutable (paused:bool) = false
let updateAllStarted() =
    WpfUtils.dispatchMessage window (fun _ ->
        updateAll.Visibility <- Visibility.Collapsed; pauseUpdate.Visibility <- Visibility.Visible; cancelUpdate.Visibility <- Visibility.Visible)
    paused <- false
    StatusesReplies.newlyAddedStatusesState.Clear()
    lastUpdateall <- DateTime.Now
let updateAllFinished() =
    setState "Update finished ..."
    WpfUtils.dispatchMessage window (fun _ ->
        updateAll.Visibility <- Visibility.Visible; pauseUpdate.Visibility <- Visibility.Collapsed; cancelUpdate.Visibility <- Visibility.Collapsed)
    MessageBox.Show("Conversations updated") |> ignore
    printfn "\n\n------------Update all done-------\n\n"
    cts.Dispose()
    cts <- null
let updateAllCancelled() =
    setState "Cancelled ..."
    WpfUtils.dispatchMessage window (fun _ ->
        updateAll.Visibility <- Visibility.Visible; pauseUpdate.Visibility <- Visibility.Collapsed; cancelUpdate.Visibility <- Visibility.Collapsed; continueUpdate.Visibility <- Visibility.Collapsed)
    printfn "\n\n------------Update all done-------"
    printfn "-- Cancelled --\n\n"
    cts.Dispose()
    cts <- null
let updateAllPaused() =
    paused <- true
    setState "Paused ..."
    pauseUpdate.Visibility <- Visibility.Collapsed; continueUpdate.Visibility <- Visibility.Visible
let updateAllContinue() =
    paused <- false
    setState "Continue ..."
    pauseUpdate.Visibility <- Visibility.Visible; continueUpdate.Visibility <- Visibility.Collapsed
cancelUpdate.Click.Add(fun _ ->
    if cts <> null then cts.Cancel()
    else log Error "Cancellation token is null"
    async {
        addNewlyFoundConversations()
        addNewlyFoundStatuses()
    } |> Async.Start
)
pauseUpdate.Click.Add(fun _ ->
    updateAllPaused()
)
continueUpdate.Click.Add(fun _ ->
    updateAllContinue()
)
updateAll.Click.Add(fun _ -> 
    cts <- new CancellationTokenSource()
    updateAllStarted()
    let compute = async {
        // called for each conversation root
        let rec update (statusIds: int64 list) = async {
            if paused then
                do! Async.Sleep(1000)
                return! update statusIds
            else 
                match statusIds with 
                | [] -> ()
                | statusId::rest ->
                    let! limitSafe = Twitter.twitterLimits.AsyncIsSafeToQueryTwitter()
                    if limitSafe then 
                        let status = ConversationState.conversationsState.GetConversation(statusId)
                        setState (sprintf "Updating status %s - %d" status.UserName status.StatusId)
                        do! getAsyncConversationUpdate (controlsCache.[statusId]) status
                        return! update rest
                    else
                        do! Async.Sleep(1000)
                        setState (sprintf "Search waiting, %A" (System.DateTime.Now))
                        return! update statusIds
        }
        let ids = ConversationState.conversationsState.GetConversations()
                    |> List.map (fun s -> s.StatusId)
                    |> List.sortBy (fun s -> -s)
        for id in ids do showConversationWillBeProcessed (controlsCache.[id].Wrapper)
        do! update ids

        addNewlyFoundConversations()
        addNewlyFoundStatuses()
        updateAllFinished()
    }
    let compCanc = Async.TryCancelled(compute, (fun _ -> updateAllCancelled()))
    Async.Start(compCanc, cts.Token)
)

[<assembly: System.Reflection.AssemblyTitle("TwitterConversation")>]
[<assembly: System.Runtime.InteropServices.Guid("2d58c139-c06e-42e5-bd91-2ff7c0c01c543")>]
()

printfn "starting app"
[<System.STAThread>]
(new Application()).Run(window) |> ignore