module Twitter

open System
open System.Xml
open Status
open Utils

type rateInfo = {
    remainingHits : int
    hourlyLimit : int
    resetTimeSec : int
    //resetTimeDate : DateTime type="datetime">2010-12-22T22:20:05+00:00</reset-time>
}

let xml2rateInfo (xml:XmlNode) =
    { remainingHits = xpathValue "/hash/remaining-hits" xml |> IntOrDefault
      hourlyLimit = xpathValue "/hash/hourly-limit" xml |> IntOrDefault
      resetTimeSec = xpathValue "/hash/reset-time-in-seconds" xml |> IntOrDefault }

type LimitState = {
    StandardRequest : rateInfo option
    SearchLimit : DateTime option // date when it is safe to continue searching
}

type TwitterLimitsMessages =
| StartLimitChecking
| UpdateLimit
| UpdateSearchLimit of Net.HttpStatusCode * Net.WebHeaderCollection
| GetLimits of AsyncReplyChannel<LimitState>


type TwitterLimits() =
    let getRateLimit() =
        try 
            let url = "http://api.twitter.com/1/account/rate_limit_status.xml"
            let xml = new XmlDocument()
            match OAuth.requestTwitter url with
             | None -> xml.LoadXml("")
             | Some(text, _, _)  -> xml.LoadXml(text)
            Some(xml2rateInfo xml)
        with ex ->
            lerrp "{0}" ex
            None

    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec asyncUpdateLoop() =
                async { do! Async.Sleep(5000) } |> Async.RunSynchronously
                ldbg "Sulimit"
                mbox.Post(UpdateLimit)
                asyncUpdateLoop()

            let rec loop limits = async {
                // first try to process GetLimits messages
                 let! res = mbox.TryScan((function
                    | GetLimits(chnl) -> Some(async {
                             ldbgp "Twitter mailbox - GetLimits {0}" mbox.CurrentQueueLength
                             chnl.Reply(limits)
                             return limits })
                    | _ -> None
                ), 5)

                match res with
                | Some limits -> 
                    return! loop limits
                | None -> 
                    ldbgp "Twitter mailbox - after GetLimits {0}" mbox.CurrentQueueLength
                    let! msg = mbox.Receive()
                    ldbgp "Twitter mailbox message: {0}" msg
                    match msg with
                    | UpdateLimit ->
                        return! loop( { limits with StandardRequest = getRateLimit() })
                    | UpdateSearchLimit(statusCode, headers) ->
                        let status = statusCode |> int
                        try 
                            if (status <> 420) then
                                ldbgp "Status code of search response is {0}" status
                                return! loop { limits with SearchLimit = None }
                            else
                                let retryAfter = headers.["Retry-After"] |> Double.TryParse
                                match retryAfter with
                                |(true, num) -> 
                                    lerrp "Search rate limit reached. Retry-After is {0}" num
                                    return! loop { limits with SearchLimit = Some(DateTime.Now.AddSeconds(num)) }
                                | _ -> 
                                    lerrp "Unable to parse response Retry-After {0}" headers
                                    return! loop limits
                        with ex ->
                            lerrp "Excepting when parsing search limit {0}" ex
                            return! loop(limits)
                    | GetLimits(chnl) ->
                        ldbg "Get limits"
                        chnl.Reply(limits)
                        return! loop(limits)
                    | StartLimitChecking ->
                        return! loop(limits)}

            mbox.Scan(fun msg ->
                match msg with
                | StartLimitChecking -> 
                    Some(async{
                        log Debug "Starting Twitter mailbox"
                        async { asyncUpdateLoop() } |> Async.Start
                        return! loop( { StandardRequest = getRateLimit(); SearchLimit = None })
                    })
                | _ -> None)
        )
    let limits2str limits =
        let standard = 
            match limits.StandardRequest with
            | Some(rate) -> sprintf "%d/%d, " rate.remainingHits rate.hourlyLimit
            | None -> "?, "
        let search = 
            match limits.SearchLimit with
            | Some(date) -> "search disabled until: " + date.ToShortTimeString()
            | _  -> "search ok"
        standard + search
    do
        mbox.Error.Add(fun exn -> lerrp "{0}" exn)
    member x.Start() = mbox.Post(StartLimitChecking)
    member x.UpdateLimit() = mbox.Post(UpdateLimit)
    member x.UpdateSearchLimit(statusCode, headers) = mbox.Post(UpdateSearchLimit(statusCode, headers))
    member x.GetLimits() = mbox.PostAndReply(GetLimits)
    member x.GetLimitsString() = mbox.PostAndReply(GetLimits) |> limits2str
    member x.IsSafeToQueryTwitter() = 
        let l = async { return! x.AsyncGetLimits() } |> Async.RunSynchronously
        match l.StandardRequest with
        | Some(x) when x.remainingHits > 0 -> 
            match l.SearchLimit with
            | Some(date) when date > DateTime.Now -> false
            | _ -> true
        | _ -> false

    member x.AsyncGetLimits() = mbox.PostAndAsyncReply(GetLimits)
    /// returns true if the search/normal limits are not reached and if
    /// the normal limits are greater than Settings.MinRateLimit
    member x.AsyncIsSafeToQueryTwitter() = async {
        let! res = x.AsyncGetLimits()
        match res.StandardRequest with
        | Some(x) when x.remainingHits > Settings.MinRateLimit -> 
            match res.SearchLimit with
            | Some(date) when date > DateTime.Now -> return false
            | _ -> return true
        | _ -> return false
    }

let twitterLimits = new TwitterLimits()

let private newStatusDownloaded = new Event<StatusSource*status>()
let NewStatusDownloaded = newStatusDownloaded.Publish

let getStatus source (id:Int64) =
    linfop "Get status {0}" id
    match StatusDb.statusesDb.ReadStatusWithId(id) with
     | Some(status) -> 
        linfop "Status {0} from db" id
        Some(status)
     | None -> 
        let limits = async { return! twitterLimits.AsyncGetLimits() } |> Async.RunSynchronously
        match limits.StandardRequest with
        | None -> 
            None
        | Some(rateInfo) when rateInfo.remainingHits < Settings.MinRateLimit -> 
            None
        | Some(rateInfo) -> 
            linfop "Downloading {0}" id // todo: store status in db!
            let formatter = sprintf "http://api.twitter.com/1/statuses/show/%d.json"
            match OAuth.requestTwitter (formatter id) with
             | Some(text, _, _) -> let xml = text |> convertJsonToXml
                                   match xml.SelectSingleNode("/root") with
                                   |null -> log Error (sprintf "status for %d is empty!" id)
                                            None
                                   |node -> let status = xml2Status node
                                            newStatusDownloaded.Trigger(source, status)
                                            linfop "Downloaded {0}" id
                                            Some(status)
             | None -> None
let getStatusOrEmpty source (id:Int64) =
    match getStatus source id with
    |Some(s) -> s
    |None -> Status.getEmptyStatus()

let search name (sinceId:Int64) =
    linfop "searching from {0}" sinceId
    let emptyResult() =
        let xml = new XmlDocument()
        xml.LoadXml("<results></results>")
        xml
    let search_() =
        //unreliable - Twitter doesn't index all tweets, to:... not working well
        //let url = sprintf "http://search.twitter.com/search.json?to=%s&since_id=%d&rpp=100&result_type=recent" name sinceId
        let url = sprintf "http://search.twitter.com/search.json?q=%%40%s&since_id=%d&rpp=100&result_type=recent" name sinceId
        match OAuth.requestTwitter url with
         | Some(text, statusCode, headers) -> twitterLimits.UpdateSearchLimit(statusCode, headers)
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
     | Some(text, _, _) -> xml.LoadXml(text)
    xml
    
let mentionsStatuses (fromStatusId:Int64) = 
    linfop "Get mentions from {0}" fromStatusId
    let from = if fromStatusId < 1000L then 1000L else fromStatusId
    let url = sprintf "http://api.twitter.com/1/statuses/mentions.xml?since_id=%d" from
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None
     | Some("", _, _) -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some(text, _, _)  -> xml.LoadXml(text)
    xml
    
let retweets (fromStatusId:Int64) =
    linfop "Get retweets from {0}" fromStatusId
    let from = if fromStatusId < 1000L then 1000L else fromStatusId
    let url = sprintf "http://api.twitter.com/1/statuses/retweeted_to_me.xml?since_id=%d&count=100" from
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None
     | Some("", _, _) -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some(text, _, _)  -> xml.LoadXml(text)
    xml

let publicStatuses() = 
    let url = "http://api.twitter.com/1/statuses/public_timeline.xml"
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some(text, _, _)  -> xml.LoadXml(text)
    xml
    
let currentUser() =
    let url = "http://api.twitter.com/1/account/verify_credentials.xml"
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None -> failwith "Unable to get info for current user"
     | Some(text, _, _)  -> xml.LoadXml(text)
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
       |> Seq.map xml2Status
let extractRetweets xpath retweetsXml =
    retweetsXml
       |> xpathNodes xpath
       |> Seq.cast<XmlNode> 
       |> Seq.map xml2Retweet
let private loadNewFriendsStatuses maxId =
    friendsStatuses maxId |> extractStatuses "//statuses/status"  |> Seq.toList
let private loadNewMentionsStatuses maxId =
    mentionsStatuses maxId |> extractStatuses "//statuses/status" |> Seq.toList
let loadNewRetweets maxId =
    retweets maxId |> extractRetweets "//statuses/status" |> Seq.toList
    
let loadNewPersonalStatuses() =
    linfo "Loading new personal statuses"
    let max = StatusDb.statusesDb.GetLastTwitterStatusId()
    linfop "Max statusId is {0}. Loading from that" max
    let newStatuses = 
        let statusesCache = new System.Collections.Generic.Dictionary<Int64, status*StatusSource>()
        let statusToCache source status =
            statusesCache.[status.StatusId] <- (status,source)

        // store statuses i cache
        (loadNewFriendsStatuses max) @ (loadNewMentionsStatuses max) |> List.iter (statusToCache Status.Timeline)

        // store retweets in cache; if there is the status already contained, that means that there is the status on timeline and somebody retweeted
        // this status will be stored with source Timeline and retweet info will be appended; retweet that is not in timeline has source Retweet
        loadNewRetweets max 
        |> List.iter (fun retweet -> if statusesCache.ContainsKey(retweet.StatusId) then
                                        statusToCache Status.Timeline retweet
                                     else
                                        statusToCache Status.Retweet retweet)

        // publish collection without duplicates
        statusesCache.Values |> Seq.toList |> List.sortBy (fun (status,_) -> status.Date)

    StatusDb.statusesDb.SaveStatuses(newStatuses)

    if newStatuses.Length > 0 then
        let getStatusId status =
            match status.RetweetInfo with
            | Some(info) -> info.RetweetId
            | None -> status.StatusId
        newStatuses 
        |> List.map (fun (status,_) -> status) 
        |> List.maxBy getStatusId 
        |> doAndRet (fun status -> linfop "Storing last status Id: {0}" status.StatusId)
        |> StatusDb.statusesDb.UpdateLastTwitterStatusId
    newStatuses
    
let loadPublicStatuses() =
    let newStatuses = 
      publicStatuses() 
       |> extractStatuses "//statuses/status"
       |> Seq.toList
       |> List.sortBy (fun status -> status.Date)
    newStatuses |> List.iter (fun s -> StatusDb.statusesDb.SaveStatus(Status.Public, s))
    newStatuses

let getStatusId status =
    status.StatusId
let sameId status1 status2 =
    status1.StatusId = status2.StatusId
let isParentOf parent status =
    parent.StatusId = status.ReplyTo