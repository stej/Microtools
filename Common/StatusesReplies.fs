module StatusesReplies

open System
open System.Xml
open Microsoft.FSharp.Collections
open Status
open Utils
open Twitter

let private statusAdded = new Event<status>()
let StatusAdded = statusAdded.Publish
let private someChildrenLoaded = new Event<status>()
let SomeChildrenLoaded = someChildrenLoaded.Publish
let private loadingStatusReplyTree = new Event<status>()
let LoadingStatusReplyTree = loadingStatusReplyTree.Publish
                
let loadSavedReplyTree initialStatus = 
    let rec addReplies status = 
        let replies = StatusDb.statusesDb.ReadStatusReplies status.StatusId
        //if sleep then System.Threading.Thread.Sleep(300)
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
let rootConversations baseStatuses (statuses: status list) =
    // function that processes one status and either adds it to the list (if it is not a reply)
    // 2) or if there is no reply parent, loads reply parent (and its parent, ...) and adds it to the list
    // 3) or if there is reply parent, adds the status as a child
    // For step 2) - after several iterations when finding parent, parent might be found, so the subtree is just added to its children
    let addStatusOrRootConversation resStatuses currStatus =
        if currStatus.ReplyTo = -1L then 
            log Debug (sprintf "Status %s %d is not reply" currStatus.UserName currStatus.StatusId)
            List.append resStatuses [currStatus] // join current statuses with this one (no conversation); ugly - other way??
        else
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
            append currStatus
    statuses |> List.fold addStatusOrRootConversation baseStatuses


let private extractStatuses xpath statusesXml =
    statusesXml
       |> xpathNodes xpath
       |> Seq.cast<XmlNode> 
       |> Seq.map xml2Status
       
let private loadNewFriendsStatuses maxId =
    friendsStatuses maxId |> extractStatuses "//statuses/status"  |> Seq.toList
let private loadNewMentionsStatuses maxId =
    mentionsStatuses maxId |> extractStatuses "//statuses/status" |> Seq.toList
    
let loadNewPersonalStatuses() =
    log Info "Loading new personal statuses"
    let max = StatusDb.statusesDb.GetLastTwitterStatusId()
    printf "Max statusId is %d. Loading from that" max
    let newStatuses = 
        let friends = loadNewFriendsStatuses max
        let friendsset = Set.ofList [for s in friends -> s.StatusId]
        // mentions that aren't also in friends list
        let filteredMentions = loadNewMentionsStatuses max |> List.filter (fun s -> not (friendsset.Contains(s.StatusId)))
        
        friends @ filteredMentions |> List.sortBy (fun status -> status.Date)

    StatusDb.statusesDb.SaveStatuses(Status.Timeline, newStatuses)
    if newStatuses.Length > 0 then
        newStatuses |> List.maxBy (fun status -> status.StatusId) |> StatusDb.statusesDb.UpdateLastTwitterStatusId
    newStatuses
    
let loadPublicStatuses() =
    let newStatuses = 
      publicStatuses() 
       |> extractStatuses "//statuses/status"
       |> Seq.toList
       |> List.sortBy (fun status -> status.Date)
    newStatuses |> List.iter (fun s -> StatusDb.statusesDb.SaveStatus(Status.Public, s))
    newStatuses