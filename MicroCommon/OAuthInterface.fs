module OAuthInterface

type IOAuth =
    abstract checkAccessTokenFile : unit -> unit
    abstract registerOnTwitter : unit -> unit
    abstract requestTwitter : string -> (string * System.Net.HttpStatusCode * System.Net.WebHeaderCollection) option

let mutable oAuthAccess:IOAuth = 
    { new IOAuth with
        member x.checkAccessTokenFile() = OAuth.checkAccessTokenFile()
        member x.registerOnTwitter() = OAuth.registerOnTwitter()
        member x.requestTwitter(url) = OAuth.requestTwitter url
    }