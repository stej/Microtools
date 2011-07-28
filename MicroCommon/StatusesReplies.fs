module StatusesReplies

open System
open System.Xml
open Microsoft.FSharp.Collections
open Status
open Utils
open Twitter
open DbFunctions
open System.Collections.Generic

type NewlyFoundRepliesMessages =
| AddStatus of statusInfo
| GetNewReplies of statusInfo * int64 seq * AsyncReplyChannel<statusInfo seq>
| GetCachedStatus of int64 * AsyncReplyChannel<statusInfo option>
| Clear

/// Storage with new unexpected replies.
/// Unexpected means that when checking a conversation, all current replies are found. However, later when checking other conversation,
/// new replies to the previous one may be found. It is because the application doesn't search for replies, but for mentions. 
/// Example: consider this conversation tree
/// Bart -- Jane -- Paul
///      |- Kr   -- ...
/// Matt -- Paul -- James
///      |- ...
/// When downloading replies (~mentions) for conversation with root Bart, all current replies are found. Then suppose that immediatelly 
/// after that someone replies to Paul and then application starts checking for replies to Matt. Because app searches for mentions, reply to
/// Paul from first conversation is found as well. So it is stored later and will be added to first conversation.
/// Why later and not immediatelly? In case that when checking first conversation even Jane didn't respond, so there is only Kr responding, then it 
/// is not obvious that Paul is responding to Bart's conversation. it could be found (downloading parent until possible), but we would ran out of limits very soon)
type NewlyFoundReplies() =
    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop replies = async {
                let! msg = mbox.Receive()
                ldbgp "Newly found replies, message: {0}" msg
                match msg with
                | Clear ->
                    return! loop (new Dictionary<int64, statusInfo>())
                | AddStatus(toAdd) ->
                    if replies.ContainsKey(toAdd.Status.StatusId) then
                        return! loop replies
                    else
                        ldbgp "Added. Count of replies collected: {0}" (replies.Count+1)
                        replies.[toAdd.Status.StatusId] <- toAdd
                        return! loop replies
                | GetNewReplies(parent, withoutChildrenIds, chnl) ->
                    let childrenSet = withoutChildrenIds |> Set.ofSeq
                    let ret = replies.Values
                              |> Seq.filter (isParentOf parent)                            // filter by parent
                              |> Seq.filter (extractStatus >> getStatusId >> childrenSet.Contains >> not)   // filter out those from childrenSet
                    chnl.Reply(ret)
                    return! loop replies
                | GetCachedStatus(statusId, chnl) ->
                    //let status = replies |> List.filter (fun s -> s.Status.StatusId = statusId) // use
                    match replies.TryGetValue(statusId) with
                    | true, sInfo -> chnl.Reply(Some(sInfo))
                    | false, _ -> chnl.Reply(None)
                    return! loop replies
            }
            ldbg "Starting NewlyFoundReplies"
            loop (new Dictionary<int64, statusInfo>())
        )
    do
        mbox.Error.Add(fun exn -> lerrp "{0}" exn)
    member x.AddStatus(s) = mbox.Post(AddStatus(s)); s
    member x.GetNewReplies(statusInfo, withoutIds) = mbox.PostAndReply(fun reply -> GetNewReplies(statusInfo, withoutIds, reply))
    member x.GetCachedStatus(statusId) = mbox.PostAndReply(fun reply -> GetCachedStatus(statusId, reply))
    member x.Clear() = mbox.Post(Clear)

let newlyAddedStatusesState = new NewlyFoundReplies()

let private statusAdded = new Event<status>()
let StatusAdded = statusAdded.Publish
let private someChildrenLoaded = new Event<statusInfo>()
let SomeChildrenLoaded = someChildrenLoaded.Publish
let private loadingStatusReplyTree = new Event<status>()
let LoadingStatusReplyTree = loadingStatusReplyTree.Publish
                
let loadSavedReplyTree initialStatusInfo = 
    let rec addReplies sInfo = 
        let replies = dbAccess.ReadStatusReplies sInfo.Status.StatusId
        replies |> Seq.iter (fun reply -> sInfo.Children.Add(reply)
                                          statusAdded.Trigger(reply.Status))
        someChildrenLoaded.Trigger(initialStatusInfo)
        replies |> Seq.iter addReplies
        loadingStatusReplyTree.Trigger(initialStatusInfo.Status)
    addReplies initialStatusInfo
    initialStatusInfo

let findReplies initialStatus =
    // todo: refactor - takes too long for people who answer more than once - duplicate requests
    let rec findRepliesIn depth sInfo =
        let status = sInfo.Status
        let getStatusIdFromNode node =
            ldbg "status from node"
            node |> xpathValue "id" |> Int64OrDefault 
        let getStatusPreferCached statusId =
            match newlyAddedStatusesState.GetCachedStatus statusId with
            | Some(statusInfo) -> Some(statusInfo)
            | None -> getStatus Status.Search statusId

        ldbgp2 "Find repl {0}, children: {1}" status.StatusId sInfo.Children.Count
        let name, id = status.UserName, status.StatusId
        let foundMentions =
            search name id
            |> xpathNodes "//results/item" 
            |> Seq.cast<XmlNode> 
            |> Seq.toList
            |> List.map getStatusIdFromNode                                                  //get statusId
            |> List.filter (fun id -> not (StatusFunctions.DirectChildHasId sInfo id))      //filter ids not in Children still
            // here I used PSeq, but .. with hangs sometimes..
            |> Seq.map getStatusPreferCached                                                //load status from cache, db or download
            |> Seq.toList                                                                   //create list back
            |> List.filter (fun sInfo -> sInfo.IsSome)                                      //filter non-null
            |> List.map (fun sInfo -> sInfo.Value)                                          //extract statusInfo
            |> List.map newlyAddedStatusesState.AddStatus
        foundMentions |> List.iter (fun sInfo -> ldbgp2 "Mention {0} - {1}" sInfo.Status.UserName status.StatusId)
        let statuses = 
            foundMentions
            |> List.filter (fun sInfo -> sInfo.Status.ReplyTo = id)                               //get only reply to current status
        let countBefore = sInfo.Children.Count
        statuses |> List.iter (fun s -> let processedStatus = s.Status
                                        if sInfo.Children.Exists(fun s0 -> s0.Status.StatusId = processedStatus.StatusId) then
                                           lerrp2 "ERROR: exists {0} {1}" processedStatus.StatusId processedStatus.Text
                                        ldbgp "Add {0}" processedStatus.StatusId
                                        sInfo.Children.Add(s)
                                        statusAdded.Trigger(processedStatus))
        someChildrenLoaded.Trigger(initialStatus)
        sInfo.Children |> Seq.iter (fun s -> ldbgp "Call fn {0}" s.Status.StatusId
                                             findRepliesIn (depth+1) s)

    findRepliesIn 0 initialStatus
    initialStatus

/// takes some status and goes up to find root of the conversation
let rootConversation (sInfo:statusInfo) =
    let rec rootconv (sInfo: statusInfo option) = 
        match sInfo with
        | Some(statusInfo) -> if statusInfo.Status.ReplyTo = -1L then
                                sInfo
                              else 
                                let newRoot = getStatus Status.RequestedConversation statusInfo.Status.ReplyTo
                                rootconv newRoot
        | None -> None
    Some(sInfo) |> rootconv
    
// takes list of statuses
// for each status checks if it is placed in conversation. If it is, it finds the root
let rootConversations (statusDownloader: int64 -> statusInfo) baseStatuses (toRoot: statusInfo list) =
    // function that processes one status that is reply to other status and 
    // 2) or if there is no reply parent in resStatuses, loads reply parent (and its parent, ...) and adds it to the list
    // 3) or if there is reply parent, adds the status as a child
    // For step 2) - after several iterations when finding parent, parent might be found, so the subtree is just added to its children

    // todo: rename params
    let rootConversation resStatuses (currStatus:statusInfo) =
        // try to append the currentSubtree somewhere to the resStatuses
        let rec append currentSubtree =
            let currSubtreeStatus = currentSubtree.Status
            if currSubtreeStatus.ReplyTo = -1L then
                ldbgp2 "Subtree {0} {1} is whole branch->adding to list" currSubtreeStatus.UserName currSubtreeStatus.StatusId
                List.append resStatuses [currentSubtree]     // the subtree is aded to the top, because we reached root of the conversation and it wasn't rooted yet anywhere else
            else
                // currSubtreeStatus is a status with some children, but the status has not been rooted yet
                let parent = StatusFunctions.FindStatusInConversationsById currSubtreeStatus.ReplyTo resStatuses       // is somewhere in resStatuses current status?
                match parent with
                |None -> ldbgp2 "Parent for status {0} {1} not found, will be loaded" currSubtreeStatus.UserName currSubtreeStatus.StatusId
                         let newRoot = statusDownloader currSubtreeStatus.ReplyTo    // there is no parent -> load it and add current as child
                         newRoot.Children.Add(currentSubtree)
                         append newRoot
                |Some(p) ->
                         ldbg (sprintf "Subtree %s %d found parent %s %d" currSubtreeStatus.UserName currSubtreeStatus.StatusId p.Status.UserName p.Status.StatusId)
                         p.Children.Add(currentSubtree)
                         resStatuses // return unchanged resStatuses
        match StatusFunctions.FindStatusInConversationsById currStatus.Status.StatusId resStatuses with
        |None    -> append currStatus
        |Some(_) -> ldbgp2 "Status {0} {1} already added. Skipping" currStatus.Status.UserName currStatus.Status.StatusId
                    resStatuses
    // first add root statuses (statuses that aren't replies, ReplyTo is -1)
    // thats because the algorithm is quite simple when first non-replies are added and possible replies bound later
    let baseWithPlainStatuses =
        toRoot
        |> Seq.filter (fun s -> s.Status.ReplyTo = -1L)
        |> Seq.fold (fun currStatuses currStatus -> currStatus::currStatuses) baseStatuses
    // and then root replies
    toRoot 
        |> Seq.filter (fun s -> s.Status.ReplyTo <> -1L) 
        |> Seq.fold rootConversation baseWithPlainStatuses
        
// prepared function that downloads status if needed
let rootConversationsWithDownload = rootConversations (getStatusOrEmpty Status.RequestedConversation)
let rootConversationsWithNoDownload = rootConversations (fun id ->
    match dbAccess.ReadStatusWithId(id) with
     | Some(status) -> status
     | None -> { Status = Status.getEmptyStatus()
                 Children = new ResizeArray<statusInfo>()
                 Source = Undefined })