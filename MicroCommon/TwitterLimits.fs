module TwitterLimits

open System
open System.Xml
open Status
open Utils
open DbFunctions

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
| UpdateSearchLimitFromResponse of Net.HttpStatusCode * Net.WebHeaderCollection
| UpdateStandarsLimitFromResponse of Net.HttpStatusCode * Net.WebHeaderCollection
| GetLimits of AsyncReplyChannel<LimitState>
| Stop

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
            lerrex ex "Get rate limit error"
            None

    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec asyncUpdateLoop() =
                async { do! Async.Sleep(5000) } |> Async.RunSynchronously
                //ldbg "Sulimit"
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
                    | UpdateSearchLimitFromResponse(statusCode, headers) ->
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
                            lerrex ex "Excepting when parsing search limit."
                            return! loop(limits)
                    | UpdateStandarsLimitFromResponse(_, headers) ->
                        try 
                            let remaining = headers.["X-RateLimit-Remaining"] |> int
                            let fullLimit = headers.["X-RateLimit-Limit"] |> int
                            let resetTime = headers.["X-RateLimit-Reset"] |> int
                            if remaining <= 0 then
                                lerr "Standard rate limit reached."
                            return! loop { limits with 
                                            StandardRequest =
                                            {
                                                remainingHits = remaining
                                                hourlyLimit = fullLimit
                                                resetTimeSec = resetTime 
                                            } |> Some }
                        with ex ->
                            lerrex ex "Excepting when parsing search limit."
                            return! loop(limits)
                    | GetLimits(chnl) ->
                        //ldbg "Get limits"
                        chnl.Reply(limits)
                        return! loop(limits)
                    | StartLimitChecking ->
                        return! loop(limits)
                    | Stop ->
                        return ()}

            mbox.Scan(fun msg ->
                match msg with
                | StartLimitChecking -> 
                    Some(async{
                        printfn "Starting Twitter limits mailbox"
                        ldbg "Starting Twitter limits mailbox"
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
        mbox.Error.Add(fun exn -> lerrex exn "Error in limits mailbox" )
    member x.Start() = mbox.Post(StartLimitChecking)
    member x.Stop() = mbox.Post(Stop)
    member x.UpdateLimit() = mbox.Post(UpdateLimit)
    member x.UpdateSearchLimitFromResponse(statusCode, headers) = mbox.Post(UpdateSearchLimitFromResponse(statusCode, headers))
    member x.UpdateStandarsLimitFromResponse(statusCode, headers) = mbox.Post(UpdateStandarsLimitFromResponse(statusCode, headers))
    member x.GetLimits() = 
        x.AsyncGetLimits() |> Async.RunSynchronously
    member x.GetLimitsString() = mbox.PostAndReply(GetLimits) |> limits2str
    member x.IsSafeToQueryTwitter() = 
        x.AsyncIsSafeToQueryTwitter() |> Async.RunSynchronously
    member x.IsSafeToQueryTwitterStatuses() =
        x.AsyncIsSafeToQueryTwitterStatuses() |> Async.RunSynchronously

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
    member x.AsyncIsSafeToQueryTwitterStatuses() = async {
        let! res = x.AsyncGetLimits()
        match res.StandardRequest with
        | Some(x) when x.remainingHits > Settings.MinRateLimit -> return true   // 0 was there before; Settings.MinRateLimit to make things less complicated
        | _ -> return false
    }

let twitterLimits = new TwitterLimits()