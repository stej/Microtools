module UrlResolver

open System
open UrlShortenerFunctions
open Utils

type UrlShortenerMessages =
| ResolveUrl of string * int64 * AsyncReplyChannel<string>
| ResolveUrlFromCache of string * AsyncReplyChannel<string option>

type UrlResolver(db:MediaDbInterface.IMediaDatabase) =
    /// Tries to get the long url from web and then save if it makes sense.
    /// If not successful, returns None
    let tryTranslateNew shortUrl statusId =
        match UrlShortenerFunctions.extract shortUrl with
        | NotNeeded(_) -> Some(shortUrl)
        | Failed       -> None
        | Extracted(u) -> db.SaveUrl(
                            { ShortUrl = shortUrl
                              LongUrl = u
                              Date = DateTime.Now
                              StatusId = statusId
                              Complete = true })
                          Some(u)
    let tryExpandIncomlete (urlInfo:MediaDbInterface.ShortUrlInfo) =
        printfn "Trying to expand incomplete: %s / %s" urlInfo.ShortUrl urlInfo.LongUrl
        match UrlShortenerFunctions.extract urlInfo.LongUrl with
        | NotNeeded(u) -> db.SetComplete(urlInfo.ShortUrl)
                          Some(u)
        | Failed       -> None
        | Extracted(u) -> db.UpdateExtracted(urlInfo.ShortUrl, u)
                          Some(u)

    /// Expands the url. First from db, then via downloading from web
    let loadOrDownload shortUrl statusId =
        match db.TranslateUrl(shortUrl) with
        | Some(res) ->
            if res.Complete then Some(res.LongUrl)
            else tryExpandIncomlete res
        | None ->
            tryTranslateNew shortUrl statusId
    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop memoryCache = async {
                let! msg = mbox.Receive()
                ldbgp "Url expander message: {0}" msg
                match msg with
                | ResolveUrl(shortUrl, statusId, chnl) ->
                    match Map.tryFind shortUrl memoryCache with
                    | Some(found) -> 
                        chnl.Reply(found)
                        return! loop memoryCache
                    | None -> 
                       match loadOrDownload shortUrl statusId with
                       | Some(longUrl) -> 
                            chnl.Reply(longUrl)
                            return! loop (memoryCache.Add(shortUrl, longUrl))
                       | None          -> 
                            chnl.Reply(shortUrl)
                            return! loop memoryCache

                | ResolveUrlFromCache(shortUrl, chnl) -> 
                    chnl.Reply(Map.tryFind shortUrl memoryCache)
                    return! loop memoryCache}
            ldbg "Starting Url expander"
            loop Map.empty
        )
    do
        mbox.Error.Add(fun exn -> lerrex exn "Error in url expander mailbox")

    member x.AsyncResolveUrl(shortUrl, statusId) = mbox.PostAndAsyncReply(fun reply -> ResolveUrl(shortUrl, statusId, reply))
    member x.AsyncResolveUrlFromCache(shortUrl) = mbox.PostAndAsyncReply(fun reply -> ResolveUrlFromCache(shortUrl, reply))