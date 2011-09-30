module Status

open System
open System.Xml
open System.Collections.Generic
open System.Text.RegularExpressions
open Utils

// todo: convert to enumeration
type StatusSource =
   | Timeline  // downloaded as timeline (mentions/friends)
   | RequestedConversation // requested - either user wants to check conversation where the status is placed or the status is fetched to see where timeline status is rooted
   | Search   // downloaded during search
   | Public   // public statuses
   | Retweet
   | Undefined
let statusSource2String src =
    match src with
    | Timeline -> "Timeline"
    | RequestedConversation -> "RequestedConversation"
    | Search -> "Search"
    | Public -> "Public"
    | Retweet -> "Retweet"
    | Undefined -> "Undefined"

type retweetInfo = { 
      Id : string
      RetweetId : Int64
      Date     : DateTime
      UserName : string
      UserId   : string
      UserProfileImage     : string
      UserProtected        : bool
      UserFollowersCount   : int
      UserFriendsCount     : int
      UserCreationDate     : DateTime
      UserFavoritesCount   : int
      UserOffset           : int
      UserUrl              : string
      UserStatusesCount    : int
      UserIsFollowing      : bool
      Inserted             : DateTime
}
type status = { Id : string;
                StatusId : int64;
                App : string;
                Account : string
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
                Inserted : DateTime
                RetweetInfo : retweetInfo option
              }
              override x.ToString() =
                String.Format("{0}{1}({2}) [{3}]", 
                    x.StatusId, 
                    (if x.RetweetInfo.IsSome then "(RT)" else ""),
                    x.UserName,
                    (if x.Text.Length < 40 then x.Text else (x.Text.Substring(0, 40) + "..."))
                )
              member x.IsRetweet () =
                match x.RetweetInfo with
                | None -> false
                | _ -> true
              member x.DisplayDate = if x.RetweetInfo.IsSome then x.RetweetInfo.Value.Date else x.Date
              member x.LogicalStatusId = if x.RetweetInfo.IsSome then x.RetweetInfo.Value.RetweetId else x.StatusId
and statusInfo = {
                   Status : status
                   Children : ResizeArray<statusInfo>
                   Source : StatusSource
                 }
                 override x.ToString() = String.Format("{0}-{1}", x.Status, (statusSource2String x.Source))
                 member x.ChildrenIds () =
                    x.Children |> Seq.map (fun s -> s.Status.StatusId)
                 member inline x.StatusId () =
                    x.Status.StatusId
        
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
      Inserted = DateTime.Now
      RetweetInfo = None
    }
let status2StatusInfo source maybeStatus =
    match maybeStatus with
    | None -> None
    | Some(s) ->
        { Status = s
          Children = new  ResizeArray<statusInfo>()
          Source = source
        } |> Some

let extractStatus statusInfo = 
    statusInfo.Status