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
| UpdateSearchLimit of Net.HttpWebResponse
| GetLimits of AsyncReplyChannel<LimitState>


type TwitterLimits() =
    let getRateLimit() =
        try 
            let url = "http://api.twitter.com/1/account/rate_limit_status.xml"
            let xml = new XmlDocument()
            match OAuth.requestTwitter url with
             | None -> xml.LoadXml("")
             | Some(text, _)  -> xml.LoadXml(text)
            Some(xml2rateInfo xml)
        with ex ->
            printfn "%A" ex
            None

    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec asyncUpdateLoop() =
                async { do! Async.Sleep(5000) } |> Async.RunSynchronously
                printf "Sulimit"
                mbox.Post(UpdateLimit)
                asyncUpdateLoop()

            let rec loop limits = async {
                // first try to process GetLimits messages
                 let! res = mbox.TryScan((function
                    | GetLimits(chnl) -> Some(async {
                             printf "G-"
                             log Debug (sprintf "Twitter mailbox - GetLimits %d" mbox.CurrentQueueLength)
                             chnl.Reply(limits)
                             return limits })
                    | _ -> None
                ), 5)

                match res with
                | Some limits -> 
                    return! loop limits
                | None -> 
                    log Debug (sprintf "Twitter mailbox - after GetLimits %d" mbox.CurrentQueueLength)
                    let! msg = mbox.Receive()
                    log Debug (sprintf "Twitter mailbox message: %A" msg)
                    match msg with
                    | UpdateLimit ->
                        return! loop( { limits with StandardRequest = getRateLimit() })
                    | UpdateSearchLimit(searchResponse) ->
                        let status = searchResponse.StatusCode |> int
                        try 
                            if (status <> 420) then
                                log Debug (sprintf "Status code of search response is %A" status)
                                return! loop { limits with SearchLimit = None }
                            else
                                let retryAfter = searchResponse.Headers.["Retry-After"] |> Double.TryParse
                                match retryAfter with
                                |(true, num) -> 
                                    log Error (sprintf "Search rate limit reached. Retry-After is %f" num)
                                    return! loop { limits with SearchLimit = Some(DateTime.Now.AddSeconds(num)) }
                                | _ -> 
                                    log Error (sprintf "Unable to parse response Retry-After %s" (searchResponse.Headers.ToString()))
                                    return! loop limits
                        with ex ->
                            log Error (sprintf "Excepting when parsing search limit %A" ex)
                            return! loop(limits)
                    | GetLimits(chnl) ->
                        printf "G2-"
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
    member x.Start() = mbox.Post(StartLimitChecking)
    member x.UpdateLimit() = mbox.Post(UpdateLimit)
    member x.UpdateSearchLimit(response) = mbox.Post(UpdateSearchLimit(response))
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
    log Info (sprintf "Get status %d" id)
    match StatusDb.statusesDb.ReadStatusWithId(id) with
     | Some(status) -> 
        log Info (sprintf "Status %d from db" id)
        Some(status)
     | None -> 
        let limits = async { return! twitterLimits.AsyncGetLimits() } |> Async.RunSynchronously
        match limits.StandardRequest with
        | None -> 
            None
        | Some(rateInfo) when rateInfo.remainingHits <= 0 -> 
            None
        | Some(rateInfo) -> 
            log Info (sprintf "Downloading %d" id)
            let formatter = sprintf "http://api.twitter.com/1/statuses/show/%d.json"
            match OAuth.requestTwitter (formatter id) with
             | Some(text, response) -> let xml = text |> convertJsonToXml
                                       match xml.SelectSingleNode("/root") with
                                       |null -> log Error (sprintf "status for %d is empty!" id)
                                                None
                                       |node -> let status = xml2Status node
                                                newStatusDownloaded.Trigger(source, status)
                                                log Info (sprintf "Downloaded %d" id)
                                                Some(status)
             | None -> None
let getStatusOrEmpty source (id:Int64) =
    match getStatus source id with
    |Some(s) -> s
    |None -> Status.getEmptyStatus()

let search name (sinceId:Int64) =
    log Info (sprintf "searching from %d" sinceId)
    let emptyResult() =
        let xml = new XmlDocument()
        xml.LoadXml("<results></results>")
        xml
    let search_() =
        //unreliable - Twitter doesn't index all tweets, to:... not working well
        //let url = sprintf "http://search.twitter.com/search.json?to=%s&since_id=%d&rpp=100&result_type=recent" name sinceId
        let url = sprintf "http://search.twitter.com/search.json?q=%%40%s&since_id=%d&rpp=100&result_type=recent" name sinceId
        match OAuth.requestTwitter url with
         | Some(text, response) -> twitterLimits.UpdateSearchLimit(response)
                                   convertJsonToXml text
         | None -> emptyResult()

    let limits = async { return! twitterLimits.AsyncGetLimits() } |> Async.RunSynchronously
    log Debug (sprintf "Current limits are %A" limits)
    match limits.SearchLimit with
    | None -> 
        log Debug "No search limit"; 
        search_()
    | Some(date) when date < DateTime.Now -> 
        log Debug (sprintf "Search limit is below. %A > %A" date DateTime.Now); 
        search_()
    | Some(date) -> 
        log Info (sprintf "Search limit reached. Stopped."); 
        printfn "search ---- stopped"
        emptyResult()

let friendsStatuses (fromStatusId:Int64) = 
    log Info (sprintf "Get friends from %d" fromStatusId)
    let url = sprintf "http://api.twitter.com/1/statuses/friends_timeline.xml?since_id=%d&count=3200" fromStatusId
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some("", response) -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some(text, response) -> xml.LoadXml(text)
    xml
    
let mentionsStatuses (fromStatusId:Int64) = 
    log Info (sprintf "Get mentions from %d" fromStatusId)
    let url = sprintf "http://api.twitter.com/1/statuses/mentions.xml?since_id=%d" fromStatusId
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some("", response) -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some(text, response)  -> xml.LoadXml(text)
    xml

let publicStatuses() = 
    let url = "http://api.twitter.com/1/statuses/public_timeline.xml"
    let xml = new XmlDocument()
    match OAuth.requestTwitter url with
     | None -> xml.LoadXml("<statuses type=\"array\"></statuses>")
     | Some(text, response)  -> xml.LoadXml(text)
    xml