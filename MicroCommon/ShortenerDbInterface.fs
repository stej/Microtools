module ShortenerDbInterface

open System

type ShortUrlInfo = {
    ShortUrl : string
    LongUrl : string
    Date : DateTime
    StatusId : int64
}

type IShortUrlsDatabase =
    abstract TranslateUrl : string -> ShortUrlInfo option
    abstract SaveUrl : ShortUrlInfo -> unit

let mutable urlsAccess:IShortUrlsDatabase = 
    { new IShortUrlsDatabase with
        member x.TranslateUrl(_) = failwith "not implemented"
        member x.SaveUrl(_) = failwith "not implemented"
    }