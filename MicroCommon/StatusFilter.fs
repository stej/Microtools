module StatusFilter
open System
open System.Text.RegularExpressions
open Status

open FParsec

type FilterItem = 
    | Regex of string
    | StatusText of string
    | User of string
    | UserRetweet of string
    | UserTimeline of string
    | AllTimeline
    | AllRetweets
    | FilterReference of string

let configFilters = 
    let appsettings = System.Configuration.ConfigurationManager.AppSettings
    [for k in appsettings.AllKeys do if k.StartsWith("filter-") then yield (k, appsettings.[k])]
let defaultConfigFilter =
    match configFilters with
    | [] -> ""
    | (key, filter)::a -> key
let configFiltersMap = configFilters |> Map.ofSeq

// returns info about if the status matches the filter
let matchesFilter (filters:FilterItem list) (sInfo:statusInfo) = 
    let status = sInfo.Status
    let source = sInfo.Source
    let matchItem = function 
                    | User(name) ->
                        let user = match status.RetweetInfo with 
                                    | Some(r) -> r.UserName 
                                    | None -> status.UserName
                        System.String.Compare(user, name, StringComparison.InvariantCultureIgnoreCase) = 0
                    | StatusText(text) -> 
                        let left = if text.StartsWith("*") then "" else "\\b"
                        let mid  = Regex.Escape(text.Replace("*",""))
                        let right = if text.EndsWith("*") then "" else "\\b"
                        let pattern = sprintf "%s%s%s" left mid right
                        Regex.Match(status.Text, pattern, RegexOptions.IgnoreCase).Success
                    | Regex(pattern) -> 
                        Regex.Match(status.Text, pattern, RegexOptions.IgnoreCase).Success
                    | AllRetweets ->
                        // timeline and requested conversation (even when retweeted) don't match the filter
                        match source with
                        | Timeline
                        | StatusSource.RequestedConversation -> false
                        | _ -> status.RetweetInfo.IsSome
                    | AllTimeline -> 
                        status.RetweetInfo.IsNone
                    | UserRetweet(username) ->
                        status.RetweetInfo.IsSome && status.RetweetInfo.Value.UserName = username
                    | UserTimeline(username) ->
                        status.RetweetInfo.IsNone && status.UserName = username
                    | FilterReference(name) ->
                        false
    let rec matchrec filter =
        match filter with
        | head::tail -> if matchItem head then true
                        else matchrec tail
        | [] -> false
    matchrec filters

let private filterTextParser =
    let charList2String (cl:char list) =
            cl |> List.map string
               |> String.concat ""
    let baseLetters c = 
        isLetter c || isDigit c || c = '_' || c = '-'
    let baseLettersOrStar c =
        c = '*' || baseLetters c
    let filterParser =
        let stringInApostrophes = 
            // todo: zrejme by slo i pomoci noneOf (pripadne nejakych satisfy)
            let escaped = pipe2 (pstring "\\") 
                                (anyString 1) 
                                (fun a b -> if b="'"then b else a+b)

            let normalCharSnippet = manySatisfy (fun c -> c <> ''' && c <> '\\')
            let normalOrEscapedCharSnippet  = 
                stringsSepBy normalCharSnippet escaped
            between (pstring "'") (pstring "'") normalOrEscapedCharSnippet 
        let wildcardText = pipe2 (satisfy baseLettersOrStar) 
                                 (many (noneOf " #"))
                                 (fun a b -> a.ToString()+(charList2String b)) |>> StatusText
        let regex        = pstring "#r:" >>. spaces >>. stringInApostrophes|>> (fun s -> s.Trim(''') |> Regex)
        //let regexNoApo   = pstring "#r:" >>. many1Satisfy (fun c->c <> ' ' && c <> ''')|>> Regex
        let textWithSpace= stringInApostrophes                             |>> StatusText
        let allTimeline  = pstring "timeline@all"                          |>> fun _ -> AllTimeline
        let allRetweets  = pstring "rt@all"                                |>> fun _ -> AllRetweets
        let user         = pstring "@"        >>. many1Satisfy baseLetters |>> User
        let userRetweet  = pstring "rt@"      >>. many1Satisfy baseLetters |>> UserRetweet
        let userTimeline = pstring "timeline@">>. many1Satisfy baseLetters |>> UserTimeline
        let filterRef    = pstring "#f:"      >>. many1Satisfy baseLetters |>> FilterReference
        let parsers = choice [allRetweets
                              allTimeline
                              userRetweet
                              userTimeline
                              regex
                              textWithSpace
                              wildcardText
                              user
                              filterRef]
        //spaces >>. (stringsSepBy parsers (skipAnyOf ' ')) .>> spaces .>> eof
        spaces >>. (sepBy parsers (skipAnyOf " ")) .>> spaces .>> eof
    filterParser

let private cachedFilters = new System.Collections.Generic.Dictionary<string, (FilterItem list) option>()
let rec parseFilter (text:string) = 
    let rec parseText text =
        let parsedExpressions =
            seq {
                match run filterTextParser text with
                | Success(result, _, _)   -> 
                    printfn "Success: %A" result
                    for item in result do
                        match item with
                        | FilterReference(filter) ->
                            if configFiltersMap.ContainsKey(filter) then yield! parseText configFiltersMap.[filter]
                            else failwith (sprintf "Unknown filter %s" filter)
                        | item -> yield item
                | Failure(errorMsg, _, _) ->
                    printfn "Failure: %A" errorMsg
                    failwith (sprintf "Error when parsing %s" text)
            }
        let ret = parsedExpressions |> Seq.toList
        for p in parsedExpressions do 
            match p with
            | Regex(text) -> if not (Utils.isValidRegex text) then failwith (sprintf "Text '%s' is not valid regular expression" text)
            | _ -> ()
        ret

    match cachedFilters.TryGetValue(text) with
    | true, parsedFilter -> 
        parsedFilter
    | false, _ -> 
        try 
            let parsed = parseText text
            cachedFilters.[text] <- Some(parsed)
            Some(parsed)
        with e ->
            cachedFilters.[text] <- None
            None
    
// @filterText - text with filter definition
// returns - (statusInfo -> bool) option (true = filter match)
//           None if there was error parsing the text
let getStatusFilterer (filterText:string) =  
    let filters = parseFilter (filterText.Trim())
    match filters with 
    | Some(parsedFilters) -> Some(matchesFilter parsedFilters)
    | None -> None