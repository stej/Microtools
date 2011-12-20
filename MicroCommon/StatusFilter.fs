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

// parses filter text to objects; supports also filters defined in config
// the filters in config may reference other filters from config -> possible infinite loop :)
//let rec parseFilter (text:string) = 
//    let filterParser =
//        let stringInApostrophes = 
//            // todo: zrejme by slo i pomoci noneOf (pripadne nejakych satisfy)
//            let escaped = pipe2 (pstring "\\") 
//                                (anyString 1) 
//                                (fun a b -> if b="'"then b else a+b)
//
//            let normalCharSnippet = manySatisfy (fun c -> c <> ''' && c <> '\\')
//            let normalOrEscapedCharSnippet  = 
//                stringsSepBy normalCharSnippet escaped
//            between (pstring "'") (pstring "'") normalOrEscapedCharSnippet 
//        let regex = 
//            pstring "#r:" 
//                >>. spaces
//                >>. stringInApostrophes
//                |>> (fun s -> s.Trim(''') |> Regex)
//        let simpleText   = many1Satisfy isLetter                        |>> StatusText
//        let textWithSpace= stringInApostrophes                          |>> StatusText
//        let allTimeline  = pstring "timeline@all"                       |>> ignore |>> (fun _ -> AllTimeline)
//        let allRetweets  = pstring "rt@all"                             |>> ignore |>> (fun _ -> AllRetweets)
//        let user         = pstring "@"        >>. many1Satisfy isLetter |>> User
//        let userRetweet  = pstring "rt@"      >>. many1Satisfy isLetter |>> UserRetweet
//        let userTimeline = pstring "timeline@">>. many1Satisfy isLetter |>> UserTimeline
//        let filterRef    = pstring "#f:"      >>. many1Satisfy (fun c -> isLetter c || isDigit c || c = '-' || c = '_') |>> FilterReference
//        let parsers = 
//            choice [allRetweets
//                    allTimeline
//                    userRetweet
//                    userTimeline
//                    regex
//                    textWithSpace
//                    simpleText
//                    user
//                    filterRef]
//        //spaces >>. (stringsSepBy parsers (skipAnyOf ' ')) .>> spaces .>> eof
//        spaces >>. (sepBy parsers (skipAnyOf " ")) .>> spaces .>> eof
//    match run filterParser text with
//        | Success(result, _, _)   -> printfn "Success: %A" result; Some(result)
//        | Failure(errorMsg, _, _) -> printfn "Failure: %s" errorMsg; None

let rec parseFilter (text:string) = 
    let charList2String (cl:char list) =
            cl |> List.map string
               |> String.concat ""
    let baseLetters c = 
            isLetter c || isDigit c || c = '_' || c = '-'
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
        let regex = 
            pstring "#r:" 
                >>. spaces
                >>. stringInApostrophes
                |>> (fun s -> s.Trim(''') |> Regex)
        let simpleText   = pipe2 (satisfy baseLetters) 
                                 (many (noneOf " #"))
                                 (fun a b -> a.ToString()+(charList2String b)) |>> StatusText
        let textWithSpace= stringInApostrophes                          |>> StatusText
        let allTimeline  = pstring "timeline@all"                       |>> ignore |>> (fun _ -> AllTimeline)
        let allRetweets  = pstring "rt@all"                             |>> ignore |>> (fun _ -> AllRetweets)
        let user         = pstring "@"        >>. many1Satisfy baseLetters |>> User
        let userRetweet  = pstring "rt@"      >>. many1Satisfy baseLetters |>> UserRetweet
        let userTimeline = pstring "timeline@">>. many1Satisfy baseLetters |>> UserTimeline
        let filterRef    = pstring "#f:"      >>. many1Satisfy baseLetters |>> FilterReference
        let parsers = 
            choice [allRetweets
                    allTimeline
                    userRetweet
                    userTimeline
                    regex
                    textWithSpace
                    simpleText
                    user
                    filterRef]
        //spaces >>. (stringsSepBy parsers (skipAnyOf ' ')) .>> spaces .>> eof
        spaces >>. (sepBy parsers (skipAnyOf " ")) .>> spaces .>> eof
    seq { 
        match run filterParser text with
        | Success(result, _, _)   -> 
            printfn "Success: %A" result
            for item in result do
                match item with
                | FilterReference(filter) ->
                    if configFiltersMap.ContainsKey(filter) then yield! parseFilter configFiltersMap.[filter]
                    else failwith (sprintf "Unknown filter %s" filter)
                | item -> yield item
        | Failure(errorMsg, _, _) ->
            failwith (sprintf "Error when parsing %s" text)
    }
    
// @filterText - text with filter definition
// returns - statusInfo -> bool (true = filter match)
let getStatusFilterer (filterText:string) =  
    let filters = parseFilter (filterText.Trim()) |> Seq.toList
    matchesFilter filters