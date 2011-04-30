module Status

open System
open System.Xml
open System.Collections.Generic
open System.Text.RegularExpressions
open Utils
open OAuth

type filterType =
    | UserName
    | Text
type statusFilter = (filterType * string) list 
type status = { Id : string; StatusId : Int64; App : string; Account : string
                Text : string
                Date : DateTime
                UserName: string
                UserId : string
                UserProfileImage : string
                ReplyTo : Int64
                UserProtected : bool
                UserFollowersCount : int
                UserFriendsCount : int
                UserCreationDate : DateTime
                UserFavoritesCount : int
                UserOffset : int
                UserUrl : string
                UserStatusesCount : int
                UserIsFollowing : bool
                Hidden : bool
                Inserted : DateTime
                Children : ResizeArray<status>
                //CopyId : int
              }
              // returns info about if the status matches the filter
              member x.MatchesFilter (filter:statusFilter) = 
                let matchItem = function 
                                | (UserName, text) -> System.String.Compare(x.UserName, text, StringComparison.InvariantCultureIgnoreCase) = 0
                                | (Text, text) -> let left = if text.StartsWith("*") then "" else "\\b"
                                                  let mid  = Regex.Escape(text.Replace("*",""))
                                                  let right = if text.EndsWith("*") then "" else "\\b"
                                                  let pattern = sprintf "%s%s%s" left mid right
                                                  Regex.Match(x.Text, pattern, RegexOptions.IgnoreCase).Success
                let rec matchrec filter =
                    match filter with
                    | head::tail -> if matchItem head then true
                                    else matchrec tail
                    | [] -> false
                matchrec filter
              member x.ChildrenIds () =
                x.Children |> Seq.map (fun s -> s.StatusId)

/// parses filter text to objects
let parseFilter (text:string) = 
    text.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
    |> Seq.map (fun part -> if part.StartsWith("@") then (UserName, (if part.Length > 0 then part.Substring(1) else ""))
                            else (Text, part))
    |> Seq.toList

/// returns true if the initialStatus or any of its child is equal to possibleChild
let rec containsInChildren initialStatus possibleChild =
    if initialStatus.StatusId = possibleChild.StatusId then true
    else initialStatus.Children |> Seq.exists (fun child -> containsInChildren child possibleChild)

/// function that would not be needed if Children were immutable...
/// and CopyId would not exist too :|
let rec cloneStatus status =
    //let ret = { status with CopyId = status.CopyId + 1 }   // fake
    let ret = { status with Children = new ResizeArray<status>() }   // fake
//    status.Children 
//        |> Seq.iter (fun child -> printfn "Clone %d" child.StatusId; ret.Children.Add(cloneStatus child))
    let children = status.Children.ToArray() |> Array.map cloneStatus
    ret.Children.Clear()
    ret.Children.AddRange(children)
    ret
    
let xml2Status (xml:XmlNode) = 
    let getValue xpath = xpathValue xpath xml
    { Id = null
      StatusId = getValue "id" |> Int64OrDefault
      App      = "Twitter"                                           //change?
      Account  = "stejcz"                                            // change?
      Text     = getValue "text"
      Date     = getValue "created_at" |> TwitterDateOrDefault
      UserName = getValue "user/screen_name"
      UserId   = getValue "user/name"
      UserProfileImage     = getValue "user/profile_image_url"
      ReplyTo              = getValue "in_reply_to_status_id" |> Int64OrDefault
      UserProtected        = getValue "user/protected" |> BoolOrDefault false
      UserFollowersCount   = getValue "user/followers_count" |> IntOrDefault
      UserFriendsCount     = getValue "user/friends_count" |> IntOrDefault
      UserCreationDate     = getValue "user/created_at" |> TwitterDateOrDefault
      UserFavoritesCount   = getValue "user/favourites_count" |> IntOrDefault
      UserOffset           = getValue "user/utc_offset" |> IntOrDefault
      UserUrl              = getValue "user/url"
      UserStatusesCount    = getValue "user/statuses_count" |> IntOrDefault
      UserIsFollowing      = getValue "user/following" |> BoolOrDefault false
      Hidden               = false                                      // change?
      Inserted             = DateTime.Now
      Children             = new ResizeArray<status>()
      //CopyId               = 0
    }
    
let getEmptyStatus() =
    { Id = null; 
      StatusId = -1L;
      App = ""
      Account = ""
      Text = "empty status"
      Date = DateTime.MinValue
      UserName = "empty"
      UserId = "empty"
      UserProfileImage = ".jpg"
      ReplyTo = -1L
      UserProtected = false
      UserFollowersCount = -1
      UserFriendsCount = -1
      UserCreationDate = DateTime.MinValue
      UserFavoritesCount = -1
      UserOffset = -1
      UserUrl = ""
      UserStatusesCount = -1
      UserIsFollowing = false
      Hidden = false
      Inserted = DateTime.Now
      Children = new ResizeArray<status>()
    }

//let mutable statusesCache = new Dictionary<Int64, status>()
//let storeAndRemember (s:status) =
//    statusesCache.Add(s.StatusId, s)
//    newStatusDownloaded.Trigger(s)

let printStatus root =
    let rec printStatus depth (status:status) =
        Utils.padSpaces depth
        linfo (sprintf "%s - %d, children: %d " status.UserName status.StatusId status.Children.Count)
        status.Children |> Seq.iter (fun s -> printStatus (depth+1) s)
    printStatus 0 root
    
type StatusSource =
    | Timeline  // downloaded as timeline (mentions/friends)
    | RequestedConversation // requested - either user wants to check conversation where the status is placed or the status is fetched to see where timeline status is rooted
    | Search   // downloaded during search
    | Public   // public statuses

let StatusSource2Int source =
    match source with
     | Timeline -> 1
     | Search -> 2
     | Public -> 3
     | RequestedConversation -> 4
let Int2StatusSource source =
    match source with
     | 1 -> Timeline
     | 2 -> Search
     | 3 -> Public
     | 4 -> RequestedConversation
     | _ -> failwith (sprintf "Value %A can not be converted to StatusSource" source)
     
let GetStatusIdsForNodes (statuses:status seq) =
    let rec getids (status:status) =
        seq {
            yield status.StatusId
            for child in status.Children do
                yield! getids child
        }
    seq {
        for s in statuses do yield! getids s
    }
let GetStatusIdsForNode (status:status) =
    GetStatusIdsForNodes [status]

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