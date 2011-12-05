module PreviewsState

open System
open Status
open Utils
open StatusFunctions
open System.Collections.Generic

let private mergeStatusesCollections (collection1:#seq<statusInfo>) (collection2:#seq<statusInfo>) =
    let mergeStatus (storedInfo:statusInfo) (newInfo:statusInfo) =
        let mutable replacement = storedInfo
        if not (storedInfo.IsRetweet()) && newInfo.IsRetweet() then
            ldbgp2 "Updating status {0}, adding retweet, {1}" newInfo.Status.StatusId newInfo.Status
            replacement <- { replacement with Status = newInfo.Status }
        if storedInfo.Source <> Timeline && newInfo.Source = Timeline then
            ldbgp "Updating status, changing source to Timeline" newInfo.Status.StatusId
            replacement <- { replacement with Source = Timeline }
        replacement
    ldbg "Begin merge"
    let currMap = new Dictionary<_,_>()
    for list in [collection1; collection2] do
        for sInfo in list do
            match currMap.TryGetValue(sInfo.StatusId()) with
            |false, _    -> currMap.[sInfo.StatusId()] <- sInfo
            |true, found -> currMap.[sInfo.StatusId()] <- mergeStatus found sInfo
    ldbg "End merge"
    currMap.Values

/// Takes new statuses and adds them to current list + tree
/// @currInfos - list of already added statuses
/// @currInfosWithRoots - @currInfos that are rooted; If a status is a reply, its parent is found and
///   the status is added as its child. If the parent is reply itself, the loop continues, until we find a
///   conversation root that is added to collection @currInfosWithRoots
///   - if any two statuses share the same parent, they are added to the parent's collection; there is no duplication
let private addAndRootStatuses currInfos currInfosWithRoots (toAdd:seq<statusInfo>) =
    let plain = mergeStatusesCollections currInfos toAdd |> Seq.toList

    ldbg "Begin create tree"
    let flattenedTreeMap = new Dictionary<_,_>()
    // first add to map from current tree
    Flatten currInfosWithRoots |> Seq.iter (fun sInfo -> flattenedTreeMap.[sInfo.StatusId()] <- {sInfo with Children = new ResizeArray<_>()})
    // add the ones from plain, because the statuses here are already updated (Source + RetweetInfo)
    plain                      |> Seq.iter (fun sInfo -> flattenedTreeMap.[sInfo.StatusId()] <- {sInfo with Children = new ResizeArray<_>()})
    ldbg "Begin create tree - rewrite statuses"

    // and create tree again
    let rooted = flattenedTreeMap.Values |> StatusesReplies.rootConversationsWithDownload []
    ldbg "End create tree"

    (plain, rooted)
(* possible statuses:
    s1(timeline), s1(retweeted), s2(timeline), mention(timeline), mention(retweeted) r(retweet)
    -> 
    join s1, mention:
    s1(timeline-retweeted), s2, mention(timeline-retweeted), r
    = status and the same retweeted status can arrive in any order; the retweeted status has priority
    in the same way Timeline source has biggest priority than others
*)
  
type PreviewStateMessages =
| AddStatuses of statusInfo seq * AsyncReplyChannel<unit>
| GetStatuses of AsyncReplyChannel<statusInfo list * statusInfo list>
| GetFirstStatusId of AsyncReplyChannel<Int64 option>
| ClearStatuses

type UserStatusesState() =
    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop (statuses:statusInfo list) statusesWithRoots = async {
                let! msg = mbox.Receive()
                ldbgp "Preview state message: {0}" msg
                match msg with
                | AddStatuses(toAdd, chnl) ->
                    //tady dodelat logiku
                    let newstatuses, newStatusesWithRoots = addAndRootStatuses statuses statusesWithRoots toAdd
                    ldbgp "Added. Count of statuses: {0}" newstatuses.Length
                    chnl.Reply(())
                    return! loop newstatuses newStatusesWithRoots
                | ClearStatuses ->
                    return! loop [] []
                | GetStatuses(chnl) ->
                    // debug - show only conversations
                    //chnl.Reply(statuses |> List.filter(fun s -> s.Children.Count > 0), statusesWithRoots |> List.filter(fun s -> s.Children.Count > 0))
                    chnl.Reply(statuses, statusesWithRoots)
                    return! loop statuses statusesWithRoots
                | GetFirstStatusId(chnl) ->
                    let id = if statuses.Length > 0 then 
                                let first = Flatten statuses |> Seq.map extractStatus 
                                                             |> Seq.sortBy (fun s -> s.LogicalStatusId) 
                                                             |> Seq.nth 0
                                Some(first.LogicalStatusId)
                             else None
                    chnl.Reply(id)
                    return! loop statuses statusesWithRoots }
            ldbg "Starting Preview state"
            loop [] []
        )
    do
        mbox.Error.Add(fun exn -> lerrex exn "Error in previews mailbox")
    member x.AddStatuses(s) = mbox.PostAndReply(fun reply -> AddStatuses(s, reply))
    member x.GetStatuses() = mbox.PostAndReply(GetStatuses)
    member x.GetFirstStatusId() = mbox.PostAndReply(GetFirstStatusId)
    member x.ClearStatuses() = mbox.Post(ClearStatuses)

    member x.AsyncGetStatuses() = mbox.PostAndAsyncReply(GetStatuses)
    member x.AsyncGetFirstStatusId() = mbox.PostAndAsyncReply(GetFirstStatusId)

let userStatusesState = new UserStatusesState()