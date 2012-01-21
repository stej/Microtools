module SubscriptionsConfig

open Utils
open System.Text.RegularExpressions

type SubscriptionsType =
    | Timeline
    | Mentions
    | ListSubscription of int64
    | Unknown of string

let parseSubscriptionsFromString (subscriptionsString:string) =
    let parseSubscription s =
        match s with
        | "timeline" | "Timeline" -> Timeline
        | "mentions" | "Mentions" -> Mentions
        | m -> let rmatch = Regex.Match(s, "^list\s+(?<num>\d+)\s*$")
               if rmatch.Success then
                    match System.Int64.TryParse(rmatch.Groups.["num"].Value) with
                    | true, value -> ListSubscription(value)
                    | _ -> Unknown(s)
               else
                    Unknown(s)
    subscriptionsString.Split([|';'|], System.StringSplitOptions.RemoveEmptyEntries) 
    |> Array.map (fun s -> s.Trim())
    |> Array.filter (System.String.IsNullOrEmpty >> not)
    |> Array.map parseSubscription
    |> Array.map (doAndRet (linfop "Parsed subscription: {0}"))

                

let subscriptions = 
    match Utils.Settings.GetAppSettings().["toDownload"] with 
    | null -> linfo "Default download subscriptions used"
              [|Timeline; Mentions|]
    | str ->  linfop "Parsing string {0} for subscriptions" str
              parseSubscriptionsFromString str

let GetTwitterCheckersFromSubscriptions () =
    seq {
        for s in subscriptions do
            match s with
            | Timeline ->             yield (Twitter.PersonalStatuses.friendsChecker.Check, Twitter.FriendsStatuses)
            | Mentions ->             yield (Twitter.PersonalStatuses.mentionsChecker.Check, Twitter.MentionsStatuses)
            | ListSubscription(id) -> yield (Twitter.PersonalStatuses.getListChecker(id).Check, Twitter.ListStatuses)
            | Unknown(str) ->         lerrp "String {0} is not recognized as valid download specification" str
    } |> Seq.toList
