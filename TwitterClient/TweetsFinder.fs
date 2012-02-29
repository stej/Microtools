module TweetsFinder

let private convertParsedFilterToSql filterType filterTokens =
    let i = ref -1
    filterTokens 
    |> List.map (fun t ->
        i := !i + 1
        let par = "@p" + (!i).ToString()
        match t with
        | StatusFilter.User(u) ->       Some("UserName = "+par, u, par)
        | StatusFilter.StatusText(t) -> Some("Text like "+par, "%"+t+"%", par)
        | _ -> None)
    |> List.choose id
let private parseFindFilter filter =
    match StatusFilter.parseFilter filter with
    | Some(parsed) -> Some(convertParsedFilterToSql parsed.FilterType parsed.Items)
    | None -> None

let find filter =
    match parseFindFilter filter with
    | Some(conditions) ->
        DbInterface.dbAccess.Find(conditions)
    | None ->
        Seq.empty