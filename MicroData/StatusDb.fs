module StatusDb

open System
open Status
open System.Data.SQLite
open Utils
open StatusFunctions
open DbCommon

let mutable fileName = "statuses.db"

type statusFromDb = {
    DbStatus: status
    DbRetweetInfoId: string
    DbSource: StatusSource
}

let private readRetweetInfo (rd:SQLiteDataReader) =
    { Id                 = str rd "Id"
      RetweetId          = long rd "RetweetId"
      Date               = date rd "Date"
      UserName           = str rd "UserName"
      UserId             = str rd "UserId"
      UserProfileImage   = str rd "UserProfileImage"
      UserProtected      = bol rd "UserProtected"
      UserFollowersCount = intt rd "UserFollowersCount"
      UserFriendsCount   = intt rd "UserFriendsCount"
      UserCreationDate   = date rd "UserCreationDate"
      UserFavoritesCount = intt rd "UserFavoritesCount"
      UserOffset         = intt rd "UserOffset"
      UserUrl            = str rd "UserUrl"
      UserStatusesCount  = intt rd "UserStatusesCount"
      UserIsFollowing    = bol rd "UserIsFollowing"
      Inserted           = date rd "Inserted"
    }
let private readStatus (rd:SQLiteDataReader) =
    { DbStatus = {
                  Id                 = str rd "Id"
                  StatusId           = long rd "StatusId"
                  App                = str rd "App"
                  Account            = str rd "Account"
                  Text               = str rd "Text"
                  Date               = date rd "Date"
                  UserName           = str rd "UserName"
                  UserId             = str rd "UserId"
                  UserProfileImage   = str rd "UserProfileImage"
                  ReplyTo            = long rd "ReplyTo"
                  UserProtected      = bol rd "UserProtected"
                  UserFollowersCount = intt rd "UserFollowersCount"
                  UserFriendsCount   = intt rd "UserFriendsCount"
                  UserCreationDate   = date rd "UserCreationDate"
                  UserFavoritesCount = intt rd "UserFavoritesCount"
                  UserOffset         = intt rd "UserOffset"
                  UserUrl            = str rd "UserUrl"
                  UserStatusesCount  = intt rd "UserStatusesCount"
                  UserIsFollowing    = bol rd "UserIsFollowing"
                  Inserted           = date rd "Inserted"
                  RetweetInfo        = None
      }
      DbRetweetInfoId = (str rd "RetweetInfoId")
      DbSource = (intt rd "Source" |> Int2StatusSource)}
let dbStatus2statusInfo dbStatus = 
    { Status = dbStatus.DbStatus
      Children = new ResizeArray<statusInfo>()
      Source = dbStatus.DbSource }

let executeSelect readFce (cmd:SQLiteCommand) = 
    use rd = cmd.ExecuteReader()
    let rec read (rd:SQLiteDataReader) =
        seq {
        if rd.Read() then
            yield readFce rd
            ldbg "reading status"
            yield! read rd
        }
    let result = read rd |> Seq.toList
    rd.Close()
    result

// todo: rename
let loadRetweetInfo (conn:SQLiteConnection) (retweetInfoId:string) =
    match retweetInfoId with
    | null 
    | "" -> None
    | value ->
        use cmd = conn.CreateCommand()
        cmd.CommandText <- "Select * from RetweetInfo where Id = @p1 limit 0,1"
        cmd.Parameters.Add(new SQLiteParameter("@p1", retweetInfoId)) |> ignore
        let rd = cmd.ExecuteReader()
        let ret = 
            if rd.Read() then
                Some(readRetweetInfo rd)
            else
                None
        rd.Close()
        ret
let addRetweetInfo (conn:SQLiteConnection) (dbStatus:statusFromDb) =
    match dbStatus.DbRetweetInfoId with
    | null -> dbStatus
    | id -> let status = { dbStatus.DbStatus with RetweetInfo = loadRetweetInfo conn dbStatus.DbRetweetInfoId }
            { dbStatus with DbStatus = status }

let executeSelectStatuses (cmd:SQLiteCommand) = 
    executeSelect readStatus cmd
    //|> List.map doAndRet (fun status -> if status.RetweetInfoId

type StatusesDbMessages =
//| LoadStatuses of AsyncReplyChannel<status seq>
| GetLastTimelineId of AsyncReplyChannel<Int64>
| GetLastMentionsId of AsyncReplyChannel<Int64>
| GetLastListItemId of int64 * AsyncReplyChannel<Int64>
| UpdateLastTimelineId of statusInfo
| UpdateLastMentionsId of statusInfo
| UpdateLastListItemId of int64 * statusInfo
| ReadStatusWithId of Int64 * AsyncReplyChannel<statusInfo option>
| ReadStatusReplies of Int64 * AsyncReplyChannel<statusInfo seq>
| GetRootStatusesHavingReplies of int * AsyncReplyChannel<statusInfo seq>
| GetTimelineStatusesBefore of int * Int64 * AsyncReplyChannel<statusInfo seq>
| GetStatusesFromSql of string * AsyncReplyChannel<statusInfo list>
| SaveStatus of statusInfo
| SaveStatuses of statusInfo list
| DeleteStatus of statusInfo * AsyncReplyChannel<Int64>

type StatusesDbState(file) =
    let checkLastIdRowExists (conn:SQLiteConnection) rowName =
        use cmdSel = conn.CreateCommand(CommandText = "select count(ItemId) from LastId where Name = @p1")
        addCmdParameter cmdSel "@p1" rowName
        match Convert.ToInt32(cmdSel.ExecuteScalar()) with
        | 0 -> linfop "Creating row in LastId for {0}" rowName
               use cmdIns = conn.CreateCommand(CommandText = "INSERT INTO LastId(Name, ItemId) VALUES (@p1, @p2)")
               addCmdParameter cmdIns "@p1" rowName
               addCmdParameter cmdIns "@p2" 100
               cmdIns.ExecuteNonQuery() |> ignore
        | _ -> ()

    let getLastId whatType rowName = 
        useDb file (fun conn ->
            ldbgp "Getting {0}" whatType
            checkLastIdRowExists conn rowName

            use cmd = conn.CreateCommand(CommandText = "select ItemId from LastId where Name = @p1")
            addCmdParameter cmd "@p1" rowName
            let ret = Convert.ToInt64(cmd.ExecuteScalar())
            linfop2 "{0} is {1}" whatType ret
            ret
        )
    let updateLastId rowName (lastStatus:Status.status) =
        useDb file (fun conn ->
            checkLastIdRowExists conn rowName

            let id = lastStatus.LogicalStatusId
            use cmd = conn.CreateCommand(CommandText = "Update LastId set ItemId = @p1 where Name = @p2")
            addCmdParameter cmd "@p1" id
            addCmdParameter cmd "@p2" rowName
            cmd.ExecuteNonQuery() |> ignore
        )
    let getLastTimelineId() = getLastId "Last timeline status id" "timeline"
    let getLastMentionsId() =  getLastId "Last mention id" "mentions"
    let getLastListItemId (listId:int64) =  getLastId "Last mention id" (sprintf "list-%d" listId)

    let updateLastTimelineId (lastStatus:statusInfo) = updateLastId "timeline" lastStatus.Status
    let updateLastMentionsId (lastStatus:statusInfo)  = updateLastId "mentions" lastStatus.Status
    let updateLastListItemId (listId:int64) (lastStatus:statusInfo)  = updateLastId (sprintf "list-%d" listId) lastStatus.Status

    // todo - rework - use other method
    let readStatusWithIdUseConn (conn:SQLiteConnection) (statusId:Int64) = 
        use cmd = conn.CreateCommand()
        cmd.CommandText <- "Select * from Status where StatusId = @p1 limit 0,1"
        cmd.Parameters.Add(new SQLiteParameter("@p1", statusId)) |> ignore
        let rd = cmd.ExecuteReader()
        let ret = 
          if rd.Read() then
            let statusFromDb = readStatus rd |> addRetweetInfo conn
            Some(dbStatus2statusInfo statusFromDb)
          else
            None
        rd.Close()
        ret

    // todo - rework - use other method
    let readStatusWithId (statusId:Int64) = 
        useDb file (fun conn -> 
            match readStatusWithIdUseConn conn statusId with 
            |Some(sInfo) -> Some(sInfo)
            |_ -> None)
    
    let readStatusReplies (statusId:Int64) = 
        useDb file (fun conn ->
            use cmd = conn.CreateCommand()
            cmd.CommandText <- "Select * from Status where ReplyTo = @p1"
            addCmdParameter cmd "@p1" statusId
            // todo - handler nebere count - pocitat pocet replies?
            executeSelectStatuses cmd |> List.map (addRetweetInfo conn) 
                                      |> List.map dbStatus2statusInfo
        )

    let getRootStatusesHavingReplies(maxCount) = 
        ldbgp "Getting conversation roots, count {0}" maxCount
        let res = useDb file (fun conn ->
                    use cmd = conn.CreateCommand()
                    //select distinct s.* from Status s join Status reply on s.StatusId=reply.ReplyTo and s.ReplyTo = -1 order by s.StatusId desc limit 0,@maxcount
                    cmd.CommandText <- "select s.* from Status s 
                                            where 
                                                s.ReplyTo = -1 and 
                                                (((s.Source = @sourceTimeline or s.Source = @sourceRetweet) and 
                                                  exists (select StatusId from Status s0 where s0.ReplyTo = s.StatusId))
                                                  or 
                                                  s.Source = @sourceConv)
                                            order by s.StatusId desc 
                                            limit 0, @maxcount"
                    addCmdParameter cmd "@maxcount" maxCount
                    addCmdParameter cmd "@sourceTimeline" (StatusSource2Int Status.Timeline)
                    addCmdParameter cmd "@sourceRetweet" (StatusSource2Int Status.Retweet)
                    addCmdParameter cmd "@sourceConv" (StatusSource2Int Status.RequestedConversation)
                    executeSelectStatuses cmd |> List.map (addRetweetInfo conn)
                )
        ldbgp "Getting conversation roots done, count {0}" maxCount
        res |> List.map dbStatus2statusInfo
    
    let getTimelineStatusesBefore count (statusId:Int64) = 
       ldbgp "getTimelineStatusesBefore {0}" statusId
       useDb file (fun conn ->
            use cmd = conn.CreateCommand()
            cmd.CommandText <- "Select s.*,  
                                    case when s.RetweetInfoid is null then s.StatusId else r.RetweetId end as LogicalstatusId
                                    from Status s 
                                    left join REtweetInfo r on s.RetweetInfoId=r.Id 
                                    where LogicalstatusId < @p1 and 
                                        (source = @p2 or source = @p3) 
                                    order by LogicalstatusId desc 
                                    limit 0, @p4"
            addCmdParameter cmd "@p1" statusId
            addCmdParameter cmd "@p2" (StatusSource2Int Status.Timeline)
            addCmdParameter cmd "@p3" (StatusSource2Int Status.Retweet)
            addCmdParameter cmd "@p4" count
            executeSelectStatuses cmd |> List.map ((addRetweetInfo conn) >> dbStatus2statusInfo)
        )
    let getStatusesFromSql sql = 
        ldbgp "getStatusesFromSql {0}" sql
        useDb file (fun conn ->
            use cmd = conn.CreateCommand()
            cmd.CommandText <- sql
            executeSelectStatuses cmd |> List.map ((addRetweetInfo conn) >> dbStatus2statusInfo)
        )
    let updateStatusSource source (status:status) =
        useDb file (fun conn ->
            use cmd = conn.CreateCommand(CommandText = "update Status set source = @p0 where StatusId = @p1")
            addCmdParameter cmd "@p0" (StatusSource2Int source)
            addCmdParameter cmd "@p1" status.StatusId
            cmd.ExecuteNonQuery() |> ignore
        )
        
    let readStatusSource (conn:SQLiteConnection) statusId =
        ldbgp "Get source for {0}" statusId
        use cmd = conn.CreateCommand(CommandText = "select Source as s from Status where StatusId = @statusid")
        addCmdParameter cmd "@statusid" statusId
        try
            use rd = cmd.ExecuteReader()
            if rd.Read() then 
                let res = rd.["s"] |> Convert.ToInt32 |> Int2StatusSource
                ldbgp2 "Source of {0} is {1}" statusId res
                Some(res)
            else
                None
        with ex -> 
            lerrex ex (sprintf "Unable to read source of %d" statusId)
            None
        
    let saveStatuses (statuses: statusInfo seq) =   
        let saveRetweetInfo (conn:SQLiteConnection) info =
            //alter table Status add Column RetweetInfoId varchar(128) default null
            //http://dev.twitter.com/doc/get/statuses/retweeted_to_me
            ldbg (sprintf "Save retweet info %d - %s" info.RetweetId info.UserName)
            let r = info
            let recordId = sprintf "TwRTId-%d" r.RetweetId
            match loadRetweetInfo conn recordId with
            | Some(_) -> 
                lerr (sprintf "Retweet info %d - %s already stored" info.RetweetId info.UserName)
            | None ->
                use cmd = conn.CreateCommand()
                cmd.CommandText <- "INSERT INTO RetweetInfo(
                    Id, RetweetId, Date, UserName, UserId, UserProfileImage, UserProtected, UserFollowersCount, UserFriendsCount, UserCreationDate, UserFavoritesCount, UserOffset, UserUrl, UserStatusesCount, UserIsFollowing, Inserted
                    ) VALUES(@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)"
                addCmdParameter cmd "@p1" recordId
                addCmdParameter cmd "@p2" r.RetweetId
                addCmdParameter cmd "@p3" r.Date.Ticks
                addCmdParameter cmd "@p4" r.UserName
                addCmdParameter cmd "@p5" r.UserId
                addCmdParameter cmd "@p6" r.UserProfileImage
                addCmdParameter cmd "@p7" r.UserProtected
                addCmdParameter cmd "@p8" r.UserFollowersCount
                addCmdParameter cmd "@p9" r.UserFriendsCount
                addCmdParameter cmd "@p10" r.UserCreationDate.Ticks
                addCmdParameter cmd "@p11" r.UserFavoritesCount
                addCmdParameter cmd "@p12" r.UserOffset
                addCmdParameter cmd "@p13" r.UserUrl
                addCmdParameter cmd "@p14" r.UserStatusesCount
                addCmdParameter cmd "@p15" r.UserIsFollowing
                addCmdParameter cmd "@p16" DateTime.Now.Ticks
                cmd.ExecuteNonQuery() |> ignore
            recordId

        let udpateStatusRetweetInfo (conn:SQLiteConnection) status =
            let retweetInfoId = saveRetweetInfo conn status.RetweetInfo.Value
            use cmd = conn.CreateCommand()
            cmd.CommandText <- "Update Status SET RetweetInfoId = @p0 where StatusId = @p1"
            addCmdParameter cmd "@p0" retweetInfoId
            addCmdParameter cmd "@p1" status.StatusId
            cmd.ExecuteNonQuery() |> ignore

        let addStatus (conn:SQLiteConnection) status source = 
            ldbg (sprintf "Save status %d - %s - %s" status.StatusId status.UserName status.Text)
            let s = status
            let retweetInfoId = 
                try
                    match s.RetweetInfo with
                    |Some(r) -> saveRetweetInfo conn r
                    | None -> null
                with ex ->
                    lerr (sprintf "Unable to store retweet info %d - %s: %A" status.StatusId status.Text ex)
                    null
            use cmd = conn.CreateCommand()
            cmd.CommandText <- "INSERT INTO Status(
                Id, StatusId, App, Account, Text, Date, UserName, UserId, UserProfileImage, ReplyTo, UserProtected, UserFollowersCount, UserFriendsCount, UserCreationDate, UserFavoritesCount, UserOffset, UserUrl, UserStatusesCount, UserIsFollowing, Source, Inserted, RetweetInfoId, Hidden
                ) VALUES(@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17, @p18, @p19, @p20, @p21, @p22, @p23)"
            addCmdParameter cmd "@p1" (sprintf "%s-%d" s.App s.StatusId)
            addCmdParameter cmd "@p2" s.StatusId
            addCmdParameter cmd "@p3" s.App
            addCmdParameter cmd "@p4" s.Account
            addCmdParameter cmd "@p5" s.Text
            addCmdParameter cmd "@p6" s.Date.Ticks
            addCmdParameter cmd "@p7" s.UserName
            addCmdParameter cmd "@p8" s.UserId
            addCmdParameter cmd "@p9" s.UserProfileImage
            addCmdParameter cmd "@p10" s.ReplyTo
            addCmdParameter cmd "@p11" s.UserProtected
            addCmdParameter cmd "@p12" s.UserFollowersCount
            addCmdParameter cmd "@p13" s.UserFriendsCount
            addCmdParameter cmd "@p14" s.UserCreationDate.Ticks
            addCmdParameter cmd "@p15" s.UserFavoritesCount
            addCmdParameter cmd "@p16" s.UserOffset
            addCmdParameter cmd "@p17" s.UserUrl
            addCmdParameter cmd "@p18" s.UserStatusesCount
            addCmdParameter cmd "@p19" s.UserIsFollowing
            addCmdParameter cmd "@p20" (StatusSource2Int source)
            addCmdParameter cmd "@p21" DateTime.Now.Ticks
            addCmdParameter cmd "@p22" retweetInfoId
            addCmdParameter cmd "@p23" false    // remove from db
            cmd.ExecuteNonQuery() |> ignore
        useDb file (fun conn ->
            ldbg "Save statuses start"
            for sInfo in statuses do 
                ldbgp "Save {0}" sInfo.Status
                let source = sInfo.Source
                let status = sInfo.Status
                match readStatusWithIdUseConn conn status.StatusId with
                | Some(statusDb) ->
                    let status = statusDb.Status
                    // when status is stored, and wasn't marked as "from Timeline" and now I got request to save it as Timeline, I'll
                    // update the source; Timeline has the highest priority
                    ldbgp2 "Found source {0}, request source is {1}" statusDb.Source source
                    if source = Status.Timeline && statusDb.Source <> Status.Timeline then
                        linfop2 "Stored with other source {0} - {1}. Updating.." status.UserName status.StatusId
                        updateStatusSource source status
                    else
                        linfop "Already stored {0}" (status.ToString())

                    // in cases that the stored status doesn't have retweet info and the current one to save has it, let's add it
                    if statusDb.Status.RetweetInfo.IsNone && sInfo.Status.RetweetInfo.IsSome then
                        linfo "Stored status doesn't have retweet info. New one has, updating"
                        udpateStatusRetweetInfo conn (sInfo.Status)
                | None -> 
                    ldbgp2 "Storing status {0} - {1}" status.UserName status.StatusId
                    try addStatus conn status source
                    with ex -> lerrex ex "Error when storing status"
        )

    let deleteStatus (statusInfo: statusInfo) = 
        let status = statusInfo.Status
        linfop2 "Deleting db status {0} - {1}" status.UserName status.StatusId
        useDb file (fun conn ->
            use cmd = conn.CreateCommand()
            if statusInfo.Status.RetweetInfo.IsSome then
                cmd.CommandText <- "delete from RetweetInfo where Id = @p0"
                addCmdParameter cmd "@p0" status.RetweetInfo.Value.Id
                cmd.ExecuteNonQuery() |> ignore
            
            cmd.CommandText <- "delete from Status where StatusId = @p0"
            addCmdParameter cmd "@p0" status.StatusId
            cmd.ExecuteNonQuery() |> ignore
        )
        statusInfo.StatusId()

    let mbox = MailboxProcessor.Start(fun mbox ->
            printfn "starting statuses db"
            let rec loop() = async {
                let! msg = mbox.Receive()
                ldbgp "Status db message: {0}" msg
                match msg with
                | SaveStatus(sInfo) -> 
                    saveStatuses [sInfo]
                    return! loop()
                | SaveStatuses(statuses) -> 
                    saveStatuses statuses
                    return! loop()
                | DeleteStatus(status, chnl) -> 
                    deleteStatus status |> chnl.Reply
                    return! loop()
//                | LoadStatuses(chnl) -> 
//                    chnl.Reply(loadStatuses())
//                    return! loop()
                | GetLastTimelineId(chnl) ->
                    chnl.Reply(getLastTimelineId())
                    return! loop()
                | UpdateLastTimelineId(status) ->
                    updateLastTimelineId status
                    return! loop()
                | GetLastMentionsId(chnl) ->
                    chnl.Reply(getLastMentionsId())
                    return! loop()
                | UpdateLastMentionsId(status) ->
                    updateLastMentionsId status
                    return! loop()
                | GetLastListItemId(listId, chnl) ->
                    chnl.Reply(getLastListItemId listId)
                    return! loop()
                | UpdateLastListItemId(listId, status) ->
                    updateLastListItemId listId status
                    return! loop()
                | ReadStatusWithId(id, chnl) ->
                    chnl.Reply(readStatusWithId id)
                    return! loop()
                | ReadStatusReplies(id, chnl) ->
                    chnl.Reply(readStatusReplies id)
                    return! loop()
                | GetRootStatusesHavingReplies(maxCount, chnl) ->
                    chnl.Reply(getRootStatusesHavingReplies maxCount)
                    return! loop()
                | GetTimelineStatusesBefore(count, fromId, chnl) ->
                    chnl.Reply(getTimelineStatusesBefore count fromId)
                    return! loop() 
                | GetStatusesFromSql(sql, chnl) ->
                    chnl.Reply(getStatusesFromSql(sql))
                    return! loop()
                 }
            ldbg "Starting status db"
            loop()
        )
    do
        mbox.Error.Add(fun exn -> printfn "exception: %A" exn
                                  lerrex exn "Error in status db mailbox")

    member x.DeleteStatus(status) = mbox.PostAndReply(fun reply -> DeleteStatus(status, reply))
    //member x.LoadStatuses() = mbox.PostAndReply(LoadStatuses)
    member x.GetRootStatusesHavingReplies(maxCount) = mbox.PostAndReply(fun reply -> GetRootStatusesHavingReplies(maxCount, reply))
    member x.GetTimelineStatusesBefore(count:int, fromId:Int64) = mbox.PostAndReply(fun reply -> GetTimelineStatusesBefore(count, fromId, reply))
    member x.GetStatusesFromSql(sql) = mbox.PostAndReply(fun reply -> GetStatusesFromSql(sql, reply))

    //member x.AsyncLoadStatuses() = mbox.PostAndAsyncReply(LoadStatuses)
    member x.AsyncGetLastTwitterStatusId() = mbox.PostAndAsyncReply(GetLastTimelineId)
    //member x.AsyncGetLastMentionsId() = mbox.PostAndAsyncReply(GetLastMentionsId)
    //member x.AsyncGetLastRetweetsId() = mbox.PostAndAsyncReply(GetLastRetweetsId)
    //member x.AsyncReadStatusWithId(id:Int64) = mbox.PostAndAsyncReply(fun reply -> ReadStatusWithId(id, reply))
    member x.AsyncReadStatusReplies(id:Int64) = mbox.PostAndAsyncReply(fun reply -> ReadStatusReplies(id, reply))
    member x.AsyncGetRootStatusesHavingReplies(maxCount) = mbox.PostAndAsyncReply(fun reply -> GetRootStatusesHavingReplies(maxCount, reply))
    member x.AsyncGetTimelineStatusesBefore(count, fromId) = mbox.PostAndAsyncReply(fun reply -> GetTimelineStatusesBefore(count, fromId, reply))

    interface DbInterface.IStatusesDatabase with
        member x.SaveStatus(sInfo) = mbox.Post(SaveStatus(sInfo))
        member x.SaveStatuses(statuses) = mbox.Post(SaveStatuses(statuses))

        member x.ReadStatusWithId(id:Int64) = mbox.PostAndReply(fun reply -> ReadStatusWithId(id, reply))

        member x.GetLastTimelineId() = mbox.PostAndReply(GetLastTimelineId)
        member x.GetLastMentionsId() = mbox.PostAndReply(GetLastMentionsId)
        member x.GetLastListItemId(listId) = mbox.PostAndReply(fun reply -> GetLastListItemId(listId, reply))
        member x.UpdateLastTimelineId(status) = mbox.Post(UpdateLastTimelineId(status))
        member x.UpdateLastMentionsId(status) = mbox.Post(UpdateLastMentionsId(status))
        member x.UpdateLastListItemId(listId, status) = mbox.Post(UpdateLastListItemId(listId, status))

        member x.ReadStatusReplies(id:Int64) = mbox.PostAndReply(fun reply -> ReadStatusReplies(id, reply))

let statusesDb = new StatusesDbState(fileName)