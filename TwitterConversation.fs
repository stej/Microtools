﻿module program

open System
open System.Xml
open Utils
open OAuth
open Status
open System.Windows.Threading

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

let private statusUpdated = new Event<WpfUtils.conversationControls * status * status>()
let StatusUpdated = statusUpdated.Publish

let controlsCache = new System.Collections.Concurrent.ConcurrentDictionary<Int64, WpfUtils.conversationControls>()

(*
let showConversationIsUpdating wrapperCtl =
    WpfUtils.dispatchMessage wrapperCtl (fun _ -> wrapperCtl.Background <- panelWorkingBrush)
let showConversationIsReady wrapperCtl =
    WpfUtils.dispatchMessage wrapperCtl (fun _ -> wrapperCtl.Background <- Brushes.White)
*)
    
let (showConversationIsUpdating, showConversationIsReady, showConversationWillBeProcessed) =
    let updateBackground (wrapperCtl:StackPanel) brush = 
        WpfUtils.dispatchMessage wrapperCtl (fun _ -> wrapperCtl.Background <- brush)
    ((fun ctl -> updateBackground ctl panelWorkingBrush), 
     (fun ctl -> updateBackground ctl Brushes.White),
     (fun ctl -> printfn "\n   updating"; updateBackground ctl panelWaitingBrush))
    
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
        statusUpdated.Trigger(controls, currentStatus, updatedStatus)
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
    WpfUtils.dispatchMessage window (fun _ -> let controls = WpfUtils.createConversationControls true addTo panel
                                              controlsCache.[rootStatus.StatusId] <- controls
                                              bindUpdate controls rootStatus
                                              //bindDelete controls rootStatus
                                              )
    rootStatus

let refreshOneConversation originalRootStatusBeforeUpdate rootStatus =
    let controls = controlsCache.[rootStatus.StatusId]
    WpfUtils.dispatchMessage controls.Statuses (fun _ -> 
        WpfUtils.updateConversation (containsInChildren originalRootStatusBeforeUpdate >> not) controls rootStatus)

let setNewConversationContent rootStatus =
    let controls = controlsCache.[rootStatus.StatusId]
    WpfUtils.dispatchMessage controls.Statuses (fun _ -> 
        WpfUtils.setNewConversation controls rootStatus
    )

StatusUpdated.Add(fun (controls, originalStatus, updatedStatus) ->
    WpfUtils.dispatchMessage controls.Statuses (fun _ ->
        refreshOneConversation originalStatus updatedStatus //controls
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
                                              //|> StatusesReplies.findReplies
                                              //|> ConversationState.conversationsState.UpdateConversation
                                              //|> refreshContent
               )
        setState"Done.."
    } |> Async.Start

    updateTwitterLimit |> Async.Start
)

updateAll.Click.Add(fun _ -> 
    async {
        let update statusId =
            if Twitter.twitterLimits.IsSafeToQueryTwitter() then
                let status = ConversationState.conversationsState.GetConversation(statusId)
                let controls = controlsCache.[status.StatusId]
                getAsyncConversationUpdate controls status |> Async.RunSynchronously
        ConversationState.conversationsState.GetConversations()
            |> List.map (fun s -> s.StatusId)
            |> List.sortBy (fun s -> -s)
            |> List.map (fun statusid -> showConversationWillBeProcessed (controlsCache.[statusid].Wrapper); statusid)
            |> List.iter update

        // add statuses, that were not visible, because they hadn't any children, but now, they got new children through 
        // all the searches
        readStatuses() 
                |> Seq.filter (fun status -> not (ConversationState.conversationsState.ContainsStatus(status.StatusId)))
                |> Seq.map ImagesSource.ensureStatusImage
                |> Seq.iter (fun status -> status |> addConversationCtls WpfUtils.Beginning
                                                  |> StatusesReplies.loadSavedReplyTree
                                                  |> ConversationState.conversationsState.AddConversation
                                                  |> setNewConversationContent)
        MessageBox.Show("Conversations updated") |> ignore
        printfn "\n\n------------Update all done-------\n\n"
    } |> Async.Start
)

printfn "starting app"
[<System.STAThread>]
(new Application()).Run(window) |> ignore