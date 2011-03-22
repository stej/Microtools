module StatusesReplies

open System
open System.Xml
open Microsoft.FSharp.Collections
open Status
open Utils
open Twitter

type NewlyFoundRepliesMessages =
| AddStatus of status
| GetNewReplies of status * int64 seq * AsyncReplyChannel<status seq>
| Clear

type NewlyFoundReplies() =
    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop replies = async {
                let! msg = mbox.Receive()
                Utils.log Utils.Debug (sprintf "Newly found replies, message: %A" msg)
                match msg with
                | Clear ->
                    return! loop []
                | AddStatus(toAdd) ->
                    if replies |> List.exists (fun s -> s.StatusId = toAdd.StatusId) then
                        return! loop replies
                    else
                        printfn "Added. Count of replies collected: %d" (replies.Length+1)
                        return! loop (toAdd::replies)
                | GetNewReplies(parent, withoutChildrenIds, chnl) ->
                    let childrenSet = withoutChildrenIds |> Set.ofSeq
                    let ret = replies 
                              |> List.filter (isParentOf parent)                            // filter by parent
                              |> List.filter (getStatusId >> childrenSet.Contains >> not)   // filter out those from childrenSet
                    chnl.Reply(ret)
                    return! loop replies
            }
            Utils.log Utils.Debug "Starting NewlyFoundReplies"
            loop []
        )
    member x.AddStatus(s) = mbox.Post(AddStatus(s)); s
    member x.GetNewReplies(status, withoutIds) = mbox.PostAndReply(fun reply -> GetNewReplies(status, withoutIds, reply))
    member x.Clear() = mbox.Post(Clear)
    //    member x.AsyncGetStatuses() = mbox.PostAndAsyncReply(GetStatuses)
    //    member x.AsyncGetFirstStatusId() = mbox.PostAndAsyncReply(GetFirstStatusId)

let newlyAddedStatusesState = new NewlyFoundReplies()

let private statusAdded = new Event<status>()
let StatusAdded = statusAdded.Publish
let private someChildrenLoaded = new Event<status>()
let SomeChildrenLoaded = someChildrenLoaded.Publish
let private loadingStatusReplyTree = new Event<status>()
let LoadingStatusReplyTree = loadingStatusReplyTree.Publish
                
let loadSavedReplyTree initialStatus = 
    let rec addReplies status = 
        let replies = StatusDb.statusesDb.ReadStatusReplies status.StatusId
        replies |> Seq.iter (fun reply -> status.Children.Add(reply)
                                          statusAdded.Trigger(reply))
        someChildrenLoaded.Trigger(initialStatus)
        replies |> Seq.iter (fun reply -> addReplies reply)
        loadingStatusReplyTree.Trigger(initialStatus)
    addReplies initialStatus
    initialStatus

let findReplies initialStatus =
    let rec findRepliesIn depth status =
        let getStatusIdFromNode node =
            log Debug "status from node"
            node |> xpathValue "id" |> Int64OrDefault 

        Utils.padSpaces depth
        printfn "Find repl %d, children: %d" status.StatusId status.Children.Count
        let (name, id) = status.UserName, status.StatusId
        let foundMentions =
            search name id
            |> xpathNodes "//results/item" 
            |> Seq.cast<XmlNode> 
            |> Seq.toList
            |> List.map getStatusIdFromNode                                                  //get statusId
            |> List.filter (fun id -> not (status.Children.Exists(fun s0 -> s0.StatusId = id)))//filter ids not in Children
            // here I used PSeq, but .. with hangs sometimes..
            |> Seq.map (getStatus Status.Search)                                            //load status from db or download
            |> Seq.toList                                                                   //create list back
            |> List.filter (fun status -> status.IsSome)                                     //filter non-null
            |> List.map (fun status -> status.Value)                                         //extract status
            |> List.map newlyAddedStatusesState.AddStatus
        foundMentions |> List.iter (fun status -> Utils.padSpaces depth; printfn "Mention %s - %d" status.UserName status.StatusId)
        let statuses = 
            foundMentions
            |> List.filter (fun status -> status.ReplyTo = id)                               //get only reply to current status
        let countBefore = status.Children.Count
        statuses |> List.iter (fun s -> if status.Children.Exists(fun s0 -> s0.StatusId = s.StatusId) then
                                           printfn "ERROR: exists %d %s" s.StatusId s.Text
                                        printfn "Add %d" s.StatusId
                                        status.Children.Add(s)
                                        statusAdded.Trigger(s))
        someChildrenLoaded.Trigger(initialStatus)
        status.Children |> Seq.iter (fun s -> printfn "Call fn %d" s.StatusId
                                              findRepliesIn (depth+1) s)

    findRepliesIn 0 initialStatus
    initialStatus

/// takes some status and goes up to find root of the conversation
let rootConversation (status:status) =
    let rec rootconv (status: status option) = 
        match status with
        | Some(status) -> if status.ReplyTo = -1L then Some(status)
                          else 
                            let newRoot = getStatus Status.RequestedConversation status.ReplyTo
                            rootconv newRoot
        | None -> None
    Some(status) |> rootconv
    
// takes list of statuses
// for each status checks if it is placed in conversation. If it is, it finds the root
let rootConversations baseStatuses (toRoot: status list) =
    // function that processes one status that is reply to other status and 
    // 2) or if there is no reply parent in resStatuses, loads reply parent (and its parent, ...) and adds it to the list
    // 3) or if there is reply parent, adds the status as a child
    // For step 2) - after several iterations when finding parent, parent might be found, so the subtree is just added to its children
    let rootConversation resStatuses currStatus =
        // try to append the currentSubtree somewhere to the resStatuses
        let rec append currentSubtree =
            if currentSubtree.ReplyTo = -1L then
                log Debug (sprintf "Subtree %s %d is whole branch->adding to list" currentSubtree.UserName currentSubtree.StatusId)
                List.append resStatuses [currentSubtree]     // the subtree is aded to the top, because we reached root of the conversation and it wasn't rooted yet anywhere else
            else
                // currentSubtree is a status with some children, but the status is not be rooted yet
                let parent = Status.GetStatusFromConversations currentSubtree.ReplyTo resStatuses       // is somewhere in resStatuses current status?
                match parent with
                |None -> log Debug (sprintf "Parent for status %s %d not found, will be loaded" currentSubtree.UserName currentSubtree.StatusId)
                         let newRoot = getStatusOrEmpty Status.RequestedConversation currentSubtree.ReplyTo    // there is no parent -> load it and add current as child
                         newRoot.Children.Add(currentSubtree)
                         append newRoot
                |Some(p) ->
                         log Debug (sprintf "Subtree %s %d found parent %s %d" currentSubtree.UserName currentSubtree.StatusId p.UserName p.StatusId)
                         p.Children.Add(currentSubtree)
                         resStatuses // return unchanged resStatuses
        match GetStatusFromConversations currStatus.StatusId resStatuses with
        |None    -> append currStatus
        |Some(_) -> log Debug (sprintf "Status %s %d already added. Skipping" currStatus.UserName currStatus.StatusId)
                    resStatuses
    // first add root statuses (statuses that aren't replies, ReplyTo is -1)
    // thats because the algorithm is quite simple when first non-replies are added and possible replies bound later
    let baseWithPlainStatuses =
        toRoot
        |> Seq.filter (fun s -> s.ReplyTo = -1L)
        |> Seq.fold (fun currStatuses currStatus -> currStatus::currStatuses) baseStatuses
    // and then root replies
    toRoot 
        |> Seq.filter (fun s -> s.ReplyTo <> -1L) 
        |> Seq.fold rootConversation baseWithPlainStatuses