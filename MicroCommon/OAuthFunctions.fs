module OAuthFunctions
open System
open Status
open System.Xml
open Utils

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
      RetweetInfo          = None
    }
let xml2Retweet (xml:XmlNode) = 
    let xml2RetweetInfo (xml:XmlNode) =
        let getValue xpath = xpathValue xpath xml
        { Id = null
          RetweetId            = getValue "id" |> Int64OrDefault
          Date                 = getValue "created_at" |> TwitterDateOrDefault
          UserName             = getValue "user/screen_name"
          UserId               = getValue "user/name"
          UserProfileImage     = getValue "user/profile_image_url"
          UserProtected        = getValue "user/protected" |> BoolOrDefault false
          UserFollowersCount   = getValue "user/followers_count" |> IntOrDefault
          UserFriendsCount     = getValue "user/friends_count" |> IntOrDefault
          UserCreationDate     = getValue "user/created_at" |> TwitterDateOrDefault
          UserFavoritesCount   = getValue "user/favourites_count" |> IntOrDefault
          UserOffset           = getValue "user/utc_offset" |> IntOrDefault
          UserUrl              = getValue "user/url"
          UserStatusesCount    = getValue "user/statuses_count" |> IntOrDefault
          UserIsFollowing      = getValue "user/following" |> BoolOrDefault false
          Inserted             = DateTime.Now
        }
    let getValue xpath = xpathValue xpath xml
    { Id = null
      StatusId = getValue "retweeted_status/id" |> Int64OrDefault
      App      = "Twitter"                                           //change?
      Account  = "stejcz"                                            // change?
      Text     = getValue "retweeted_status/text"
      Date     = getValue "retweeted_status/created_at" |> TwitterDateOrDefault
      UserName = getValue "retweeted_status/user/screen_name"
      UserId   = getValue "retweeted_status/user/name"
      UserProfileImage     = getValue "retweeted_status/user/profile_image_url"
      ReplyTo              = getValue "retweeted_status/in_reply_to_status_id" |> Int64OrDefault
      UserProtected        = getValue "retweeted_status/user/protected" |> BoolOrDefault false
      UserFollowersCount   = getValue "retweeted_status/user/followers_count" |> IntOrDefault
      UserFriendsCount     = getValue "retweeted_status/user/friends_count" |> IntOrDefault
      UserCreationDate     = getValue "retweeted_status/user/created_at" |> TwitterDateOrDefault
      UserFavoritesCount   = getValue "retweeted_status/user/favourites_count" |> IntOrDefault
      UserOffset           = getValue "retweeted_status/user/utc_offset" |> IntOrDefault
      UserUrl              = getValue "retweeted_status/user/url"
      UserStatusesCount    = getValue "retweeted_status/user/statuses_count" |> IntOrDefault
      UserIsFollowing      = getValue "retweeted_status/user/following" |> BoolOrDefault false
      Hidden               = false                                      // change?
      Inserted             = DateTime.Now
      RetweetInfo          = Some(xml2RetweetInfo xml)
    }