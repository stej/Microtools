module Twitter

open System
open System.Xml
open Status
open Utils
open TwitterLimits
open DbInterface

let private newStatusDownloaded = new Event<statusInfo>()
let NewStatusDownloaded = newStatusDownloaded.Publish

let getStatus source (id:Int64) =
    let convertToStatus source node = 
        let parsedStatus = OAuthFunctions.xml2Status node
        match parsedStatus with
        | Some(status) ->
            let ret = { Status = status
                        Children = new ResizeArray<statusInfo>()
                        Source = source }
            newStatusDownloaded.Trigger(ret)
            ldbgp "Downloaded {0}" id
            Some(ret)
        | None -> None

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
            match OAuthInterface.oAuthAccess.requestTwitter (formatter id) with
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

let search userName (sinceId:Int64) =
    ldbgp "searching from {0}" sinceId
    let emptyResult() =
        let xml = new XmlDocument()
        xml.LoadXml("<results></results>")
        xml
    let search_() =
        //unreliable - Twitter doesn't index all tweets, to:... not working well
        //let url = sprintf "http://search.twitter.com/search.json?to=%s&since_id=%d&rpp=100&result_type=recent" userName sinceId
        let url = sprintf "http://search.twitter.com/search.json?q=%%40%s&since_id=%d&rpp=100&result_type=recent" userName sinceId
        match OAuthInterface.oAuthAccess.requestTwitter url with
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

type PersonalStatusesType =
    | FriendsStatuses
    | MentionsStatuses
    | RetweetsStatuses

module PersonalStatuses = 
    let friendsChecker, mentionsChecker, retweetsChecker = 
        let normalizeId idGetter = 
            match idGetter() with 
            | id when id < 1000L -> 1000L
            | id -> id
        // todo: dependency on db
        let getFriendsUrl () = sprintf "http://api.twitter.com/1/statuses/home_timeline.xml?since_id=%d&count=200&include_rts=true" (normalizeId dbAccess.GetLastTimelineId)
        let getMentionsUrl () = sprintf "http://api.twitter.com/1/statuses/mentions.xml?since_id=%d&include_rts=1&count=200" (normalizeId dbAccess.GetLastMentionsId)
        let getRetweetsUrl () = sprintf "http://api.twitter.com/1/statuses/retweeted_to_me.xml?since_id=%d&count=100" (normalizeId dbAccess.GetLastRetweetsId)
        let canQuery = twitterLimits.IsSafeToQueryTwitterStatuses
        new TwitterStatusesChecker.Checker(FriendsStatuses, (OAuthFunctions.xml2StatusOrRetweet >> status2StatusInfoWithUnknownTimelineSource), getFriendsUrl, canQuery),
        new TwitterStatusesChecker.Checker(MentionsStatuses, (OAuthFunctions.xml2Status >> (status2StatusInfo Timeline)), getMentionsUrl, canQuery),
        new TwitterStatusesChecker.Checker(RetweetsStatuses, (OAuthFunctions.xml2Retweet >> (status2StatusInfo Retweet)), getRetweetsUrl, canQuery)

    let saveStatuses requestType statuses =
        let getLogicalStatusId (sInfo:statusInfo) = sInfo.Status.LogicalStatusId
        match statuses with
        | Some(slist) when slist |> Seq.isEmpty |> not -> 
            dbAccess.SaveStatuses(slist)
            let latestStatus = slist |> List.maxBy getLogicalStatusId
            match requestType with
            | FriendsStatuses -> dbAccess.UpdateLastTimelineId(latestStatus)
            | MentionsStatuses -> dbAccess.UpdateLastMentionsId(latestStatus)
            | RetweetsStatuses -> dbAccess.UpdateLastRetweetsId(latestStatus)
        | _ -> ()

let getStatusId status =
    status.StatusId
let sameId status1 status2 =
    status1.StatusId = status2.StatusId
let isParentOf parentInfo statusInfo =
    parentInfo.Status.StatusId = statusInfo.Status.ReplyTo

(*************************************************************************************************************************************)

(*let publicStatuses() = 
    let url = "http://api.twitter.com/1/statuses/public_timeline.xml"
    let xml = new XmlDocument()
    match OAuthInterface.oAuthAccess.requestTwitter url with
     | None -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some(text, statusCode, headers)  -> 
        twitterLimits.UpdateStandarsLimitFromResponse(statusCode, headers)
        xml.LoadXml(text)
    xml

let currentUser() =
    let url = "http://api.twitter.com/1/account/verify_credentials.xml"
    let xml = new XmlDocument()
    match OAuthInterface.oAuthAccess.requestTwitter url with
     | None -> failwith "Unable to get info for current user"
     | Some(text, statusCode, headers)  -> 
        twitterLimits.UpdateStandarsLimitFromResponse(statusCode, headers)
        xml.LoadXml(text)
    xml
*)

(*let twitterLists() = 
    let user = xpathValue "/user/screen_name" (currentUser())
    let url = sprintf "http://api.twitter.com/1/%s/lists.xml" user
    let xml = new XmlDocument()
    match OAuthInterface.oAuthAccess.requestTwitter url with
     | None -> xml.LoadXml("<lists_list><lists type=\"array\"/></lists_list>")
     | Some(text, _, _)  -> xml.LoadXml(text)
    xml
*)

(*let loadPublicStatuses() =
    let newStatuses = 
      publicStatuses() 
       |> extractStatuses "//statuses/status"
       |> Seq.toList
       |> List.sortBy (fun status -> status.Date)
    newStatuses |> List.iter (fun s -> dbAccess.SaveStatus(Status.Public, s))
    newStatuses
*)