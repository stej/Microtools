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

// returns info about if the status matches the filter
let matchesFilter (filter:statusFilter) (status:status) = 
    let matchItem = function 
                    | (UserName, text) ->
                        let user = match status.RetweetInfo with | Some(r) -> r.UserName | None -> status.UserName
                        System.String.Compare(user, text, StringComparison.InvariantCultureIgnoreCase) = 0
                    | (Text, text) -> 
                        let left = if text.StartsWith("*") then "" else "\\b"
                        let mid  = Regex.Escape(text.Replace("*",""))
                        let right = if text.EndsWith("*") then "" else "\\b"
                        let pattern = sprintf "%s%s%s" left mid right
                        Regex.Match(status.Text, pattern, RegexOptions.IgnoreCase).Success
                    | (RTs,_) -> 
                        status.RetweetInfo.IsSome // todo - include timeline statuses
                    | (TimelineStatuses, _) -> 
                        status.RetweetInfo.IsNone
    let rec matchrec filter =
        match filter with
        | head::tail -> if matchItem head then true
                        else matchrec tail
        | [] -> false
    matchrec filter

/// parses filter text to objects
let parseFilter (text:string) = 
    text.Split([|' '|], StringSplitOptions.RemoveEmptyEntries)
    |> Seq.map (fun part -> if part = "allRT" then
                                (RTs, null)
                            else if part = "allTimeline" then
                                (TimelineStatuses, null)
                            else if part.StartsWith("@") then 
                                (UserName, (if part.Length > 0 then part.Substring(1) else ""))
                            else 
                                (Text, part))
    |> Seq.toList