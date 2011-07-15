module StatusFilter
open System
open System.Text.RegularExpressions
open Status

type filterType =
   | UserName
   | Text
   | RTs
   | TimelineStatuses


type statusFilter = (filterType * string) list 

let configFilters = 
    let appsettings = System.Configuration.ConfigurationManager.AppSettings
    [for k in appsettings.AllKeys do if k.StartsWith("filter-") then yield (k, appsettings.[k])]
let defaultConfigFilter =
    match configFilters with
    | [] -> ""
    | (key, filter)::a -> key
let configFiltersMap = configFilters |> Map.ofSeq

// returns info about if the status matches the filter
let matchesFilter (filter:statusFilter) (sInfo:statusInfo) = 
    let status = sInfo.Status
    let source = sInfo.Source
    let matchItem = function 
                    | (UserName, text) ->
                        let user = match status.RetweetInfo with 
                                    | Some(r) -> r.UserName 
                                    | None -> status.UserName
                        System.String.Compare(user, text, StringComparison.InvariantCultureIgnoreCase) = 0
                    | (Text, text) -> 
                        let left = if text.StartsWith("*") then "" else "\\b"
                        let mid  = Regex.Escape(text.Replace("*",""))
                        let right = if text.EndsWith("*") then "" else "\\b"
                        let pattern = sprintf "%s%s%s" left mid right
                        Regex.Match(status.Text, pattern, RegexOptions.IgnoreCase).Success
                    | (RTs,_) ->
                        // timeline and requested conversation (even when retweeted) don't match the filter
                        match source with
                        | Timeline
                        | StatusSource.RequestedConversation -> false
                        | _ -> status.RetweetInfo.IsSome
                    | (TimelineStatuses, _) -> 
                        status.RetweetInfo.IsNone
    let rec matchrec filter =
        match filter with
        | head::tail -> if matchItem head then true
                        else matchrec tail
        | [] -> false
    matchrec filter

// parses filter text to objects; supports also filters defined in config
// the filters in config may reference other filters from config -> possible infinite loop :)
let rec parseFilter (text:string) = 
    seq { 
        for part in text.Split([|' '|], StringSplitOptions.RemoveEmptyEntries) do
            if configFiltersMap.ContainsKey(part) then yield! parseFilter configFiltersMap.[part]
            else if part = "allRT" then yield (RTs, null)
            else if part = "allTimeline" then yield (TimelineStatuses, null)
            else if part.StartsWith("@") then  yield (UserName, (if part.Length > 0 then part.Substring(1) else ""))
            else yield (Text, part)
    } |> Seq.toList