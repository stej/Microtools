module TextSplitter

open Utils

type TextFragment =
    | FragmentWords of string
    | FragmentUrl of string
    | FragmentUserMention of string
    | FragmentHash of string

let private regexUrl = new System.Text.RegularExpressions.Regex(    
                                "(?<user>@\w+)|" + 
                                "(?<hash>#\w+)|" +
                                "(?<url>https?:(?://|\\\\)+(?:[\w\-]+\.)+[\w]+(?:/?$|[\w\d:#@%/;$()~_?+\-=\\\.&*]*[\w\d:#@%/;$()~_+\-=\\&*]))")

let splitText text =
    let getFragment part =
        let mtch = regexUrl.Match(part)
        if mtch.Success then
            ldbgp "Parsed url: {0}" part
            let matchGroups = mtch.Groups
            if matchGroups.["url"].Success       then FragmentUrl(part)
            else if matchGroups.["user"].Success then FragmentUserMention(part)
            else if matchGroups.["hash"].Success then FragmentHash(part)
            else 
                lerr (sprintf "Url %s parsed incorrectly" part)
                FragmentWords(part)
        else
            FragmentWords(part)

    ldbgp "Parsing {0}" text
    
    regexUrl.Split(System.Web.HttpUtility.HtmlDecode(text)) 
    |> Array.filter (System.String.IsNullOrEmpty >> not)
    |> Array.map getFragment

let urlFragmentToLinkAndName = function
    | FragmentUrl(s)         -> s, s
    | FragmentUserMention(s) -> (sprintf "http://twitter.com/%s" (s.TrimStart('@')), s)
    | FragmentHash(s)        -> (sprintf "http://twitter.com/search?q=%s" (System.Web.HttpUtility.UrlEncode(s)), s)
    | x                      -> failwith ("the url fragment not supported: " + x.ToString())