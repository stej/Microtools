module UrlExpander

open System
open UrlShortenerFunctions
open Utils

type UrlShortenerMessages =
| ResolveUrl of string * int64 * AsyncReplyChannel<string>

type UrlExpander() =
    let tryTranslate shortUrl statusId =
        match UrlShortenerFunctions.extract shortUrl with
        | NotNeeded(_) -> shortUrl
        | Failed       -> shortUrl
        | Extracted(u) -> ShortenerDbInterface.urlsAccess.SaveUrl(
                            { ShortUrl = shortUrl
                              LongUrl = u
                              Date = DateTime.Now
                              StatusId = statusId })
                          u
    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop () = async {
                let! msg = mbox.Receive()
                ldbgp "Url expander message: {0}" msg
                match msg with
                | ResolveUrl(shortUrl, statusId, chnl) ->
                    match ShortenerDbInterface.urlsAccess.TranslateUrl(shortUrl) with
                    | Some(res) -> chnl.Reply(res.LongUrl)
                    | None      -> chnl.Reply(tryTranslate shortUrl statusId)
                    return! loop ()}
            ldbg "Starting Url expander"
            loop ()
        )
    do
        mbox.Error.Add(fun exn -> lerrex exn "Error in url expander mailbox")

    member x.AsyncResolveUrl(shortUrl, statusId) = mbox.PostAndAsyncReply(fun reply -> ResolveUrl(shortUrl, statusId, reply))

let urlExpander = new UrlExpander()