module StatusFunctions
open Status

/// function that would not be needed if Children were immutable...
let rec cloneStatus status =
    let ret = { status with Children = new ResizeArray<status>() }   // fake
    let children = status.Children.ToArray() |> Array.map cloneStatus
    ret.Children.Clear()
    ret.Children.AddRange(children)
    ret

(*let printStatus root =
    let rec printStatus depth (status:status) =
        Utils.padSpaces depth
        linfo (sprintf "%s - %d, children: %d " status.UserName status.StatusId status.Children.Count)
        status.Children |> Seq.iter (fun s -> printStatus (depth+1) s)
    printStatus 0 root*)

let StatusSource2Int source =
    match source with
     | Timeline -> 1
     | Search -> 2
     | Public -> 3
     | RequestedConversation -> 4
     | Retweet -> 5

let Int2StatusSource source =
    match source with
     | 1 -> Timeline
     | 2 -> Search
     | 3 -> Public
     | 4 -> RequestedConversation
     | 5 -> Retweet
     | _ -> failwith (sprintf "Value %A can not be converted to StatusSource" source)
     
(*let GetStatusIdsForNodes (statuses:status seq) =
    let rec getids (status:status) =
        seq {
            yield status.StatusId
            for child in status.Children do
                yield! getids child
        }
    seq {
        for s in statuses do yield! getids s
    }*)
//let GetStatusIdsForNode (status:status) =
    //GetStatusIdsForNodes [status]

// takes status with children and returns Some(status) with StatusId equal to statusId or None if there is no such status in the tree
let GetStatusFromConversation statusId tree =
    let rec get_ currStatus =
        if currStatus.StatusId = statusId then
            Some(currStatus)
        else
            let rec traverseChildren (children: status list) =
                match children with
                | [] -> None
                | c::oth -> match get_ c with 
                            |Some(status) -> Some(status)
                            |None -> traverseChildren oth
            currStatus.Children |> Seq.toList |> traverseChildren 
    get_ tree
let GetStatusFromConversations statusId (trees: status list) =
    trees |> List.tryPick (GetStatusFromConversation statusId)
    
let Flatten (statuses:status list) =
    let rec flatten (statuses_: status seq) =
        seq {
            for s in statuses_ do
                yield s
                yield! (flatten s.Children)
        }
    flatten statuses

let getId status =
    status.StatusId

let GetNewestDisplayDateFromConversation (status:status) =
    Flatten [status] |> Seq.map (fun status -> status.DisplayDate)
                     |> Seq.sortBy (fun date -> -date.Ticks) 
                     |> Seq.nth 0