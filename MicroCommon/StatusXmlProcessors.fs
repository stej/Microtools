﻿module StatusXmlProcessors
open System
open Status
open System.Xml
open Utils
open ShortenerDbInterface

let xml2Status (xml:XmlNode) = 
    ldbgp "Parsing status {0}" (xml.OuterXml)
    let getValue xpath = xpathValue xpath xml
    let parsed = 
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
          Inserted             = DateTime.Now
          RetweetInfo          = None
        }
    if parsed.StatusId = Int64Default then None else Some(parsed)
    
let xml2Retweet (xml:XmlNode) = 
    ldbgp "Parsing retweet {0}" (xml.OuterXml)
    let xml2RetweetInfo (xml:XmlNode) =
        let getValue xpath = xpathValue xpath xml
        let parsed = 
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
        if parsed.RetweetId = Int64Default then None else Some(parsed)
    let getValue xpath = xpathValue xpath xml
    let parsed = 
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
          Inserted             = DateTime.Now
          RetweetInfo          = xml2RetweetInfo xml
        }
    if parsed.StatusId = Int64Default || parsed.RetweetInfo.IsNone then 
        None 
    else 
        Some(parsed)

let xml2StatusOrRetweet (xml:XmlNode) = 
    if xml.SelectSingleNode("retweeted_status") <> null then
        xml2Retweet xml
    else
        xml2Status xml

module ExtraProcessors =
    module Url =
        let private parseUrlEntity (sInfo:statusInfo) (xml:XmlNode) =
            { ShortUrl = xpathValue "url" xml
              LongUrl = xpathValue "expanded_url" xml
              Date = DateTime.Now
              StatusId = sInfo.Status.StatusId 
              Complete = false}
        let extractEntities (sInfo:statusInfo) (xml:XmlNode) =
            let xpath = if sInfo.IsRetweet() then "retweeted_status/entities/urls/url"
                        else "entities/urls/url"
            xpathNodes xpath xml
            |> Seq.map (parseUrlEntity sInfo)
        let storeEntities entities =
            entities |> Seq.iter urlsAccess.SaveIncompleteUrl
        let ParseShortUrlsAndStore (sInfo:statusInfo) (xml:XmlNode) =
            extractEntities sInfo xml |> storeEntities

    let mutable Processors : (statusInfo -> XmlNode -> unit) list = []