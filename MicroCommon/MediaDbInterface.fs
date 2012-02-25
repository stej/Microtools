module MediaDbInterface

open System

type ShortUrlInfo = {
    ShortUrl : string
    LongUrl : string
    Date : DateTime
    StatusId : int64
    Complete : bool
}

type PhotoInfo = {
    Id : string
    ShortUrl : string
    LongUrl : string
    ImageUrl : string
    Date : DateTime
    StatusId : int64
    Sizes : string
}

type IMediaDatabase =
    abstract TranslateUrl : string -> ShortUrlInfo option
    abstract SaveUrl : ShortUrlInfo -> unit
    abstract SaveIncompleteUrl : ShortUrlInfo -> unit
    abstract SetComplete : string -> unit
    abstract UpdateExtracted : string * string -> unit

    abstract SavePhoto : PhotoInfo -> unit

let mutable urlsAccess:IMediaDatabase = 
    { new IMediaDatabase with
        member x.TranslateUrl(_) = failwith "not implemented"
        member x.SaveUrl(_) = failwith "not implemented"
        member x.SaveIncompleteUrl(_) = failwith "not implemented"
        member x.SetComplete(_) = failwith "not implemented"
        member x.UpdateExtracted(_, _) = failwith "not implemented"
        member x.SavePhoto(_) = failwith "not implemented"
    }