module Status

open System
open System.Xml
open System.Collections.Generic
open System.Text.RegularExpressions
open Utils

type StatusSource =
   | Timeline  // downloaded as timeline (mentions/friends)
   | RequestedConversation // requested - either user wants to check conversation where the status is placed or the status is fetched to see where timeline status is rooted
   | Search   // downloaded during search
   | Public   // public statuses
   | Retweet

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
                RetweetInfo : retweetInfo option
              }
              override x.ToString() =
                String.Format("{0}{1}({2}) [{3}]", 
                    x.StatusId, 
                    (if x.RetweetInfo.IsSome then "(RT)" else ""),
                    x.UserName,
                    (if x.Text.Length < 40 then x.Text else (x.Text.Substring(0, 40) + "..."))
                )
              member x.ChildrenIds () =
                x.Children |> Seq.map (fun s -> s.StatusId)
              member x.IsRetweet () =
                match x.RetweetInfo with
                | None -> false
                | _ -> true
              member x.DisplayDate = if x.RetweetInfo.IsSome then x.RetweetInfo.Value.Date else x.Date
              member x.LogicalStatusId = if x.RetweetInfo.IsSome then x.RetweetInfo.Value.RetweetId else x.StatusId
        
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
      RetweetInfo = None
    }