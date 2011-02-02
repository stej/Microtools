module PreviewsState

open System
open Status

(*
let private displayedStatuses = new ResizeArray<status>()
let private statusComparer = { new System.Collections.Generic.IComparer<status> with 
                                member x.Compare(status1, status2) = status1.StatusId.CompareTo(status2.StatusId) }

let addStatuses (statuses: status seq) =
    statuses 
      |> Seq.filter (fun status -> not (displayedStatuses.Exists(fun s0 -> s0.StatusId = status.StatusId)))
      |> Seq.iter (fun status -> printfn "Add %d" status.StatusId; displayedStatuses.Add(status) |> ignore)
    displayedStatuses.Sort(statusComparer)
    displayedStatuses        
  
let getStatusesToDisplay() =
    displayedStatuses |> Seq.toList

let getFirstStatusId() =
    if displayedStatuses.Count > 0 then Some(displayedStatuses.[0].StatusId)
    else None
*)

let addAndRootStatuses currStatuses currStatusesWithRoots toAdd =
    /// todo - make map
    let flattened = Flatten currStatusesWithRoots
    /// Seq.filter - statuses that should be added and are not contained even in conversation roots        
    let rooted = toAdd 
                    |> Seq.filter (fun status -> not (flattened |> Seq.exists (fun s0 -> s0.StatusId = status.StatusId)))
                    |> Seq.map (fun status -> printfn "Add %d - %s" status.StatusId status.UserName; status)
                    |> Seq.toList
                    |> StatusesReplies.rootConversations currStatusesWithRoots
    let plain = currStatuses @ (Seq.toList toAdd)
    (plain, rooted)
  
type PreviewStateMessages =
| AddStatuses of status seq
| GetStatuses of AsyncReplyChannel<status list * status list>
| GetFirstStatusId of AsyncReplyChannel<Int64 option>
| ClearStatuses

type UserStatusesState() =
    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop statuses statusesWithRoots = async {
                let! msg = mbox.Receive()
                Utils.log Utils.Debug (sprintf "Preview state message: %A" msg)
                match msg with
                | AddStatuses(toAdd) ->
                    let newstatuses, newStatusesWithRoots = addAndRootStatuses statuses statusesWithRoots toAdd
                    printfn "Added. Count of statuses: %d" newstatuses.Length
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
            Utils.log Utils.Debug "Starting Preview state"
            loop [] []
        )
    member x.AddStatuses(s) = mbox.Post(AddStatuses(s))
    member x.GetStatuses() = mbox.PostAndReply(GetStatuses)
    member x.GetFirstStatusId() = mbox.PostAndReply(GetFirstStatusId)
    member x.ClearStatuses() = mbox.Post(ClearStatuses)

    member x.AsyncGetStatuses() = mbox.PostAndAsyncReply(GetStatuses)
    member x.AsyncGetFirstStatusId() = mbox.PostAndAsyncReply(GetFirstStatusId)

let userStatusesState = new UserStatusesState()