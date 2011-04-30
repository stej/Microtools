module PreviewsState

open System
open Status
open Utils

/// Takes new statuses and adds them to current list + tree
/// @currStatuses - list of already added statuses
/// @currStatusesWithRoots - @currStatuses that are rooted; If a status is a reply, its parent is found and
///   the status is added as its child. If the parent is reply itself, the loop continues, until we find a
///   conversation root that is added to collection @currStatusesWithRoots
///   - if any two statuses share the same parent, they are added to the parent's collection; there is no duplication
let addAndRootStatuses currStatuses currStatusesWithRoots toAdd =
    let currIds = Flatten currStatusesWithRoots 
                  |> Seq.map getId 
                  |> Set.ofSeq
    /// Seq.filter - statuses that should be added and are not contained even in conversation roots        
    let rooted = toAdd 
                    |> Seq.filter (fun status -> not (currIds.Contains(status.StatusId)))
                    |> Seq.map (fun status -> ldbgp2 "Add {0} - {1}" status.StatusId status.UserName; status)
                    |> Seq.toList
                    |> StatusesReplies.rootConversations currStatusesWithRoots
    let plain = currStatuses @ (Seq.toList toAdd)
    (plain, rooted)
  
type PreviewStateMessages =
| AddStatuses of status seq * AsyncReplyChannel<unit>
| GetStatuses of AsyncReplyChannel<status list * status list>
| GetFirstStatusId of AsyncReplyChannel<Int64 option>
| ClearStatuses

type UserStatusesState() =
    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop statuses statusesWithRoots = async {
                let! msg = mbox.Receive()
                ldbgp "Preview state message: {0}" msg
                match msg with
                | AddStatuses(toAdd, chnl) ->
                    let newstatuses, newStatusesWithRoots = addAndRootStatuses statuses statusesWithRoots toAdd
                    ldbgp "Added. Count of statuses: {0}" newstatuses.Length
                    chnl.Reply(())
                    return! loop newstatuses newStatusesWithRoots
                | ClearStatuses ->
                    return! loop [] []
                | GetStatuses(chnl) ->
                    chnl.Reply(statuses, statusesWithRoots)
                    return! loop statuses statusesWithRoots
                | GetFirstStatusId(chnl) ->
                    let id = if statuses.Length > 0 then 
                                let first = Flatten statuses |> Seq.sortBy (fun s -> s.StatusId) |> Seq.nth 0
                                Some(first.StatusId)
                             else None
                    chnl.Reply(id)
                    return! loop statuses statusesWithRoots }
            ldbg "Starting Preview state"
            loop [] []
        )
    do
        mbox.Error.Add(fun exn -> lerrp "{0}" exn)
    member x.AddStatuses(s) = mbox.PostAndReply(fun reply -> AddStatuses(s, reply))
    member x.GetStatuses() = mbox.PostAndReply(GetStatuses)
    member x.GetFirstStatusId() = mbox.PostAndReply(GetFirstStatusId)
    member x.ClearStatuses() = mbox.Post(ClearStatuses)

    member x.AsyncGetStatuses() = mbox.PostAndAsyncReply(GetStatuses)
    member x.AsyncGetFirstStatusId() = mbox.PostAndAsyncReply(GetFirstStatusId)

let userStatusesState = new UserStatusesState()