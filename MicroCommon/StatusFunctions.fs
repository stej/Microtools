module StatusFunctions
open Status

/// function that would not be needed if Children were immutable...
let rec cloneStatus statusInfo =
    let children = statusInfo.Status.Children.ToArray() |> Array.map cloneStatus

    let ret = { 
        statusInfo with Status = { statusInfo.Status with Children = new ResizeArray<statusInfo>() }   // fake
    }
    ret.Status.Children.Clear()
    ret.Status.Children.AddRange(children)
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
     | Undefined -> 1000

let Int2StatusSource source =
    match source with
     | 1 -> Timeline
     | 2 -> Search
     | 3 -> Public
     | 4 -> RequestedConversation
     | 5 -> Retweet
     | 1000 -> Undefined
     | _ -> failwith (sprintf "Value %A can not be converted to StatusSource" source)
     
// takes status with children and returns Some(status) with StatusId equal to statusId or None if there is no such status in the tree
let FindStatusById statusId tree =
    let rec get_ currStatus =
        if currStatus.Status.StatusId = statusId then
            Some(currStatus)
        else
            let rec traverseChildren (children: statusInfo list) =
                match children with
                | [] -> None
                | c::oth -> match get_ c with 
                            |Some(status) -> Some(status)
                            |None -> traverseChildren oth
            currStatus.Status.Children |> Seq.toList |> traverseChildren 
    get_ tree
let FindStatusInConversationsById statusId (trees: statusInfo list) =
    trees |> List.tryPick (FindStatusById statusId)
    
let Flatten (statuses:statusInfo seq) =
    let rec flatten (statuses_: statusInfo seq) =
        seq {
            for s in statuses_ do
                yield s
                yield! (flatten s.Status.Children)
        }
    flatten statuses

let getId status =
    status.StatusId

let GetNewestDisplayDateFromConversation (sInfo:statusInfo) =
    Flatten [sInfo] |> Seq.map (fun info -> info.Status.DisplayDate)
                    |> Seq.sortBy (fun date -> -date.Ticks) 
                    |> Seq.nth 0

let DirectChildHasId sInfo id =
     sInfo.Status.Children |> Seq.exists (fun child -> child.Status.StatusId = id) //filter ids not in Children

let AnyChildHasId sInfo id =
    let flattened = sInfo.Status.Children |> Flatten
    flattened |> Seq.exists (fun child -> child.Status.StatusId = id) //filter ids not in Children or deeper in children