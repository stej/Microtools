module Twitter

open System
open System.Xml
open Status
open Utils
open DbFunctions
open TwitterLimits

let private newStatusDownloaded = new Event<statusInfo>()
let NewStatusDownloaded = newStatusDownloaded.Publish

let getStatus source (id:Int64) =
    let convertToStatus source node = 
        let ret = { Status = OAuthFunctions.xml2Status node
                    Children = new ResizeArray<statusInfo>()
                    Source = source }
        newStatusDownloaded.Trigger(ret)
        ldbgp "Downloaded {0}" id
        Some(ret)

    ldbgp "Get status {0}" id
    match dbAccess.ReadStatusWithId(id) with
     | Some(sInfo) -> 
        ldbgp "Status {0} from db" id
        Some(sInfo)
     | None -> 
        let limits = twitterLimits.GetLimits()
        match limits.StandardRequest with
        | None -> 
            None
        | Some(rateInfo) when rateInfo.remainingHits < Settings.MinRateLimit -> 
            None
        | Some(rateInfo) -> 
            ldbgp "Downloading {0}" id // todo: store status in db!
            let formatter = sprintf "http://api.twitter.com/1/statuses/show/%d.json"
            match OAuth.requestTwitter (formatter id) with
             | Some(text, System.Net.HttpStatusCode.Forbidden, _) -> 
                                   let ret = { Status = { getEmptyStatus() with StatusId = id; Text = "forbidden" }
                                               Children = new ResizeArray<statusInfo>()
                                               Source = source }
                                   newStatusDownloaded.Trigger(ret)
                                   ldbg "Downloaded forbidden status"
                                   Some(ret)
             | Some(text, _, _) -> try 
                                       let xml = text |> convertJsonToXml
                                       match xml.SelectSingleNode("/root") with
                                       |null -> lerrp "status for {0} is empty!" id
                                                None
                                       |node -> convertToStatus source node 
                                   with ex -> 
                                       lerrex ex (sprintf "Unable to parse status %s" text)
                                       None
                                            
             | None -> None
let getStatusOrEmpty source (id:Int64) =
    match getStatus source id with
    |Some(s) -> s
    |None -> { Status = Status.getEmptyStatus()
               Children = new ResizeArray<statusInfo>()
               Source = Undefined }

let search name (sinceId:Int64) =
    ldbgp "searching from {0}" sinceId
    let emptyResult() =
        let xml = new XmlDocument()
        xml.LoadXml("<results></results>")
        xml
    let search_() =
        //unreliable - Twitter doesn't index all tweets, to:... not working well
        //let url = sprintf "http://search.twitter.com/search.json?to=%s&since_id=%d&rpp=100&result_type=recent" name sinceId
        let url = sprintf "http://search.twitter.com/search.json?q=%%40%s&since_id=%d&rpp=100&result_type=recent" name sinceId
        match OAuth.requestTwitter url with
         | Some(text, statusCode, headers) -> twitterLimits.UpdateSearchLimitFromResponse(statusCode, headers)
                                              convertJsonToXml text
         | None -> emptyResult()

    let limits = async { return! twitterLimits.AsyncGetLimits() } |> Async.RunSynchronously
    ldbgp "Current limits are {0}" limits
    match limits.SearchLimit with
    | None -> 
        ldbg "No search limit"
        search_()
    | Some(date) when date < DateTime.Now -> 
        ldbgp2 "Search limit is below. {0} > {1}" date DateTime.Now
        search_()
    | Some(date) -> 
        linfo "Search limit reached. Stopped..."
        emptyResult()

let friendsStatuses (fromStatusId:Int64) = 
    linfop "Get friends from {0}" fromStatusId
    let from = if fromStatusId < 1000L then 1000L else fromStatusId
    let url = sprintf "http://api.twitter.com/1/statuses/friends_timeline.xml?since_id=%d&count=3200" from
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None
     | Some("", _, _) -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some(text, statusCode, headers) -> 
        twitterLimits.UpdateStandarsLimitFromResponse(statusCode, headers)
        xml.LoadXml(text)
    xml
    
let mentionsStatuses (fromStatusId:Int64) = 
    linfop "Get mentions from {0}" fromStatusId
    let from = if fromStatusId < 1000L then 1000L else fromStatusId
    let url = sprintf "http://api.twitter.com/1/statuses/mentions.xml?since_id=%d" from
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None
     | Some("", _, _) -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some(text, statusCode, headers)  -> 
        twitterLimits.UpdateStandarsLimitFromResponse(statusCode, headers)
        xml.LoadXml(text)
    xml
    
let retweets (fromStatusId:Int64) =
    linfop "Get retweets from {0}" fromStatusId
    let from = if fromStatusId < 1000L then 1000L else fromStatusId
    let url = sprintf "http://api.twitter.com/1/statuses/retweeted_to_me.xml?since_id=%d&count=100" from
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None
     | Some("", _, _) -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some(text, statusCode, headers)  -> 
        twitterLimits.UpdateStandarsLimitFromResponse(statusCode, headers)
        xml.LoadXml(text)
    xml

let publicStatuses() = 
    let url = "http://api.twitter.com/1/statuses/public_timeline.xml"
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some(text, statusCode, headers)  -> 
        twitterLimits.UpdateStandarsLimitFromResponse(statusCode, headers)
        xml.LoadXml(text)
    xml

let currentUser() =
    let url = "http://api.twitter.com/1/account/verify_credentials.xml"
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None -> failwith "Unable to get info for current user"
     | Some(text, statusCode, headers)  -> 
        twitterLimits.UpdateStandarsLimitFromResponse(statusCode, headers)
        xml.LoadXml(text)
    xml

let twitterLists() = 
    let user = xpathValue "/user/screen_name" (currentUser())
    let url = sprintf "http://api.twitter.com/1/%s/lists.xml" user
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None -> xml.LoadXml("<lists_list><lists type=\"array\"/></lists_list>")
     | Some(text, _, _)  -> xml.LoadXml(text)
    xml
       
let private extractStatuses xpath statusesXml =
    statusesXml
       |> xpathNodes xpath
       |> Seq.cast<XmlNode> 
       |> Seq.map OAuthFunctions.xml2Status
let extractRetweets xpath retweetsXml =
    retweetsXml
       |> xpathNodes xpath
       |> Seq.cast<XmlNode> 
       |> Seq.map OAuthFunctions.xml2Retweet

let private loadNewFriendsStatuses maxId =
    friendsStatuses maxId |> extractStatuses "//statuses/status"  |> Seq.toList
let private loadNewMentionsStatuses maxId =
    mentionsStatuses maxId |> extractStatuses "//statuses/status" |> Seq.toList
let loadNewRetweets maxId =
    retweets maxId |> extractRetweets "//statuses/status" |> Seq.toList
    
type LoadedPersonalStatuses = {
    NewStatuses : statusInfo list
    LastFriendStatus : statusInfo option
    LastMentionStatus : statusInfo option
    LastRetweet : statusInfo option
}
let getLastStoredIds () =
    (dbAccess.GetLastTimelineId(),
     dbAccess.GetLastMentionsId(),
     dbAccess.GetLastRetweetsId())
let loadNewPersonalStatuses fIsSaveToQueryStatuses (lastTimelineId, lastMentionId, lastRetweetId) =
    linfo "Loading new personal statuses"

    let getStatusId (status:status) = status.LogicalStatusId
    let loadSomeStatuses lastId (loader:Int64 -> status list) = 
        if fIsSaveToQueryStatuses() then
            let ret = loader lastId
            if ret.Length > 0 then 
                ret, Some({ Status = ret |> List.maxBy getStatusId
                            Children = new ResizeArray<statusInfo>()
                            Source = Timeline })
            else
                ret, None
        else
            [], None
    let friends, lastF  = loadSomeStatuses lastTimelineId loadNewFriendsStatuses
    let mentions, lastM = loadSomeStatuses lastMentionId loadNewMentionsStatuses
    let retweets, lastR = loadSomeStatuses lastRetweetId loadNewRetweets

    let statusesCache = new System.Collections.Generic.Dictionary<Int64, status*StatusSource>()
    let statusToCache source status =
        statusesCache.[status.StatusId] <- (status,source)

    // store statuses i cache
    friends @ mentions |> List.iter (statusToCache Status.Timeline)

    // store retweets in cache; if there is the status already contained, that means that there is the status on timeline and somebody retweeted
    // this status will be stored with source Timeline and retweet info will be appended; retweet that is not in timeline has source Retweet
    retweets 
    |> List.iter (fun retweet -> if statusesCache.ContainsKey(retweet.StatusId) then
                                    statusToCache Status.Timeline retweet
                                    else
                                    statusToCache Status.Retweet retweet)

    { 
        // publish collection without duplicates
        NewStatuses = statusesCache.Values |> Seq.toList 
                                           |> List.sortBy (fun (status,_) -> status.DisplayDate)
                                           |> List.map (fun (status, source) -> { Status = status
                                                                                  Children = new ResizeArray<statusInfo>()
                                                                                  Source = source })
        LastFriendStatus = lastF
        LastMentionStatus = lastM
        LastRetweet = lastR
    }

let saveDownloadedStatuses (statuses: LoadedPersonalStatuses) =
    dbAccess.SaveStatuses(statuses.NewStatuses)
    if statuses.LastFriendStatus.IsSome then dbAccess.UpdateLastTimelineId(statuses.LastFriendStatus.Value)
    if statuses.LastMentionStatus.IsSome then dbAccess.UpdateLastMentionsId(statuses.LastMentionStatus.Value)
    if statuses.LastRetweet.IsSome then dbAccess.UpdateLastRetweetsId(statuses.LastRetweet.Value)
    // this print is not necessary, but ensures that all commands are executed in db agent
    printfn "Last timeline status ids: %A" (dbAccess.GetLastTimelineId())
    statuses
    
(*let loadPublicStatuses() =
    let newStatuses = 
      publicStatuses() 
       |> extractStatuses "//statuses/status"
       |> Seq.toList
       |> List.sortBy (fun status -> status.Date)
    newStatuses |> List.iter (fun s -> dbAccess.SaveStatus(Status.Public, s))
    newStatuses
*)

let getStatusId status =
    status.StatusId
let sameId status1 status2 =
    status1.StatusId = status2.StatusId
let isParentOf parentInfo statusInfo =
    parentInfo.Status.StatusId = statusInfo.Status.ReplyTo