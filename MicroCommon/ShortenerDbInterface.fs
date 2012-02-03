module ShortenerDbInterface

open System

type ShortUrlInfo = {
    ShortUrl : string
    LongUrl : string
    Date : DateTime
    StatusId : int64
    Complete : bool
}

type IShortUrlsDatabase =
    abstract TranslateUrl : string -> ShortUrlInfo option
    abstract SaveUrl : ShortUrlInfo -> unit
    abstract SaveIncompleteUrl : ShortUrlInfo -> unit
    abstract SetComplete : string -> unit
    abstract UpdateExtracted : string * string -> unit

let mutable urlsAccess:IShortUrlsDatabase = 
    { new IShortUrlsDatabase with
        member x.TranslateUrl(_) = failwith "not implemented"
        member x.SaveUrl(_) = failwith "not implemented"
        member x.SaveIncompleteUrl(_) = failwith "not implemented"
        member x.SetComplete(_) = failwith "not implemented"
        member x.UpdateExtracted(_, _) = failwith "not implemented"
    }