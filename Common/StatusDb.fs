module StatusDb

open System
open Status
open System.Data.SQLite

let mutable fileName = "statuses.db"
let private doNothingHandler _ = ()
let private doNothingHandler2 _ _ = ()

let private readStatus (rd:SQLiteDataReader) =
    let str (id:string)  = rd.[id].ToString()
    let long (id:string) = Convert.ToInt64(rd.[id])
    let intt (id:string) = Convert.ToInt32(rd.[id])
    let date (id:string) = new DateTime(long id)
    let bol (id:string)  = Convert.ToBoolean(rd.[id])
    { Id                 = str "Id"
      StatusId           = long "StatusId"
      App                = str "App"
      Account            = str "Account"
      Text               = str "Text"
      Date               = date "Date"
      UserName           = str "UserName"
      UserId             = str "UserId"
      UserProfileImage   = str "UserProfileImage"
      ReplyTo            = long "ReplyTo"
      UserProtected      = bol "UserProtected"
      UserFollowersCount = intt "UserFollowersCount"
      UserFriendsCount   = intt "UserFriendsCount"
      UserCreationDate   = date "UserCreationDate"
      UserFavoritesCount = intt "UserFavoritesCount"
      UserOffset         = intt "UserOffset"
      UserUrl            = str "UserUrl"
      UserStatusesCount  = intt "UserStatusesCount"
      UserIsFollowing    = bol "UserIsFollowing"
      Hidden             = bol "Hidden"
      Inserted           = date "Inserted"
      Children           = new ResizeArray<status>()
      //CopyId             = 0
     }

let useDb useFce = 
    use conn = new System.Data.SQLite.SQLiteConnection()
    conn.ConnectionString <- sprintf "Data Source=\"%s\"" fileName
    conn.Open()
    let result = useFce conn 
    conn.Close()
    result
    
let addCmdParameter (cmd:SQLiteCommand) (name:string) value = 
    cmd.Parameters.Add(new SQLiteParameter(name, (value :> System.Object))) |> ignore

let executeSelect readFce (cmd:SQLiteCommand) = 
    use rd = cmd.ExecuteReader()
    let rec read (rd:SQLiteDataReader) =
        seq {
        if rd.Read() then
            yield readFce rd
            printf "."
            yield! read rd
        }
    let result = read rd |> Seq.toList
    rd.Close()
    result

let executeSelectStatuses (cmd:SQLiteCommand) = 
    executeSelect readStatus cmd

type StatusesDbMessages =
| LoadStatuses of AsyncReplyChannel<status seq>
| GetLastTwitterStatusId of AsyncReplyChannel<Int64>
| UpdateLastTwitterStatusId of status
| ReadStatusWithId of Int64 * AsyncReplyChannel<status option>
| ReadStatusReplies of Int64 * AsyncReplyChannel<status seq>
| GetRootStatusesHavingReplies of int * AsyncReplyChannel<status seq>
| GetTimelineStatusesBefore of int * Int64 * AsyncReplyChannel<status seq>
| GetStatusesFromSql of string * AsyncReplyChannel<status seq>
| SaveStatus of Status.StatusSource * status
| SaveStatuses of Status.StatusSource * status list
| DeleteStatus of status

type StatusesDbState() =
    let loadStatuses loadHandler = 
        useDb (fun conn ->
            let count = 
                use cmd = conn.CreateCommand(CommandText = "Select count(Id) from Status")
                Convert.ToInt32(cmd.ExecuteScalar())
            use cmd = conn.CreateCommand()
            cmd.CommandText <- "Select * from Status"
            executeSelectStatuses cmd
        )

    let getLastTwitterStatusId() = 
        useDb (fun conn ->
            Utils.log Utils.Debug "Getting last twitter id"
            //use cmd = conn.CreateCommand(CommandText = "Select max(StatusId) from Status")
            use cmd = conn.CreateCommand(CommandText = "select TwitterStatusId from AppState")
            let ret = Convert.ToInt64(cmd.ExecuteScalar())
            Utils.log Utils.Info (sprintf "Last twitter id is %d" ret)
            ret
        )

    let updateLastTwitterStatusId (lastStatus:Status.status) =
        useDb (fun conn ->
            use cmd = conn.CreateCommand(CommandText = "Update AppState set TwitterStatusId = @p1")
            addCmdParameter cmd "@p1" lastStatus.StatusId
            cmd.ExecuteNonQuery() |> ignore
        )

    // todo - rework - use other method
    let readStatusWithId (statusId:Int64) = 
        useDb (fun conn ->
          use cmd = conn.CreateCommand()
          cmd.CommandText <- "Select * from Status where StatusId = @p1 limit 0,1"
          cmd.Parameters.Add(new SQLiteParameter("@p1", statusId)) |> ignore
          let rd = cmd.ExecuteReader()
          let ret = 
            if rd.Read() then
              Some(readStatus rd)
            else
              None
          rd.Close()
          ret
        )
    
    let readStatusReplies (statusId:Int64) = 
        useDb (fun conn ->
            use cmd = conn.CreateCommand()
            cmd.CommandText <- "Select * from Status where ReplyTo = @p1"
            addCmdParameter cmd "@p1" statusId
            // todo - handler nebere count - pocitat pocet replies?
            executeSelectStatuses cmd
        )

    let getRootStatusesHavingReplies(maxCount) = 
        printfn "Getting conversation roots, count %d" maxCount
        let res = useDb (fun conn ->
                    use cmd = conn.CreateCommand()
                    //select distinct s.* from Status s join Status reply on s.StatusId=reply.ReplyTo and s.ReplyTo = -1 order by s.StatusId desc limit 0,@maxcount
                    cmd.CommandText <- "select s.* from Status s 
                                            where 
                                                (s.ReplyTo = -1 and 
                                                 s.Source = @sourceTimeline and 
                                                 exists (select StatusId from Status s0 where s0.ReplyTo = s.StatusId))
                                                or s.Source = @sourceConv
                                            order by s.StatusId desc 
                                            limit 0, @maxcount"
                    addCmdParameter cmd "@maxcount" maxCount
                    addCmdParameter cmd "@sourceTimeline" (StatusSource2Int Status.Timeline)
                    addCmdParameter cmd "@sourceConv" (StatusSource2Int Status.RequestedConversation)
                    executeSelectStatuses cmd
                )
        printfn "Getting conversation roots done, count %d" maxCount
        res
    
    let getTimelineStatusesBefore count (statusId:Int64) = 
       Utils.log Utils.Debug (sprintf "getTimelineStatusesBefore %d" statusId)
       useDb (fun conn ->
            use cmd = conn.CreateCommand()
            cmd.CommandText <- "Select * from Status where StatusId < @p1 and source = @p2 order by StatusId desc limit 0, @p3"
            addCmdParameter cmd "@p1" statusId
            addCmdParameter cmd "@p2" (StatusSource2Int Status.Timeline)
            addCmdParameter cmd "@p3" count
            executeSelectStatuses cmd
        )
    let getStatusesFromSql sql = 
        Utils.log Utils.Debug (sprintf "getStatusesFromSql %s" sql)
        useDb (fun conn ->
            use cmd = conn.CreateCommand()
            cmd.CommandText <- sql
            executeSelectStatuses cmd
        )
    let updateStatusSource source (status:status) =
        useDb (fun conn ->
            use cmd = conn.CreateCommand(CommandText = "update Status set source = @p0 where StatusId = @p1")
            addCmdParameter cmd "@p0" (StatusSource2Int source)
            addCmdParameter cmd "@p1" status.StatusId
            cmd.ExecuteNonQuery() |> ignore
        )
        
    let readStatusSource (conn:SQLiteConnection) statusId =
        Utils.log Utils.Debug (sprintf "Get source for %d" statusId)
        use cmd = conn.CreateCommand(CommandText = "select Source as s from Status where StatusId = @statusid")
        addCmdParameter cmd "@statusid" statusId
        try
            use rd = cmd.ExecuteReader()
            if rd.Read() then 
                let res = rd.["s"] |> Convert.ToInt32 |> Int2StatusSource
                printfn "Source of %d is %A" statusId res
                Some(res)
            else
                None
        with ex -> 
            printfn "Unable to read source of %d %A" statusId ex
            None
        
    let saveStatuses source (statuses: status seq) = 
        useDb (fun conn ->
                (*let exists = 
                    use cmd = conn.CreateCommand()
                    cmd.CommandText <- (sprintf "Select count(Id) from Status where Id = '%s-%d'" status.App status.StatusId)
                    Convert.ToInt32(cmd.ExecuteScalar()) > 0*)
              
            let addStatus status = 
                Utils.log Utils.Debug (sprintf "Save status %d - %s - %s" status.StatusId status.UserName status.Text)
                let s = status
                use cmd = conn.CreateCommand()
                cmd.CommandText <- "INSERT INTO Status(
                    Id, StatusId, App, Account, Text, Date, UserName, UserId, UserProfileImage, ReplyTo, UserProtected, UserFollowersCount, UserFriendsCount, UserCreationDate, UserFavoritesCount, UserOffset, UserUrl, UserStatusesCount, UserIsFollowing, Hidden, Source, Inserted
                    ) VALUES(@p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17, @p18, @p19, @p20, @p21, @p22)"
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
                addCmdParameter cmd "@p20" s.Hidden
                addCmdParameter cmd "@p21" (StatusSource2Int source)
                addCmdParameter cmd "@p22" DateTime.Now.Ticks
                cmd.ExecuteNonQuery() |> ignore

            for status in statuses do 
                match readStatusSource conn status.StatusId with
                // kdyz je status ulozeny, je timelinovy a byl ulozeny nejak jinak, pak mu to nastavim - timeline je nejvyssi priorita
                | Some(src) -> printfn "Found source %A, request source is %A" src source
                               if source = Status.Timeline && src <> Status.Timeline then
                                    printfn "Stored with other source %s - %d" status.UserName status.StatusId
                                    updateStatusSource source status
                               else
                                    printfn "Already stored %s - %d" status.UserName status.StatusId
                | None -> printfn "Storing status %s - %d" status.UserName status.StatusId
                          try addStatus status
                          with ex -> printfn "%A" ex
                      
        )

    let deleteStatus (status: status) = 
        printfn "Deleting db status %s - %d" status.UserName status.StatusId
        useDb (fun conn ->
            use cmd = conn.CreateCommand()
            cmd.CommandText <- "delete from Status where StatusId = @p0"
            addCmdParameter cmd "@p0" status.StatusId
            cmd.ExecuteNonQuery() |> ignore
        )

    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop() = async {
                let! msg = mbox.Receive()
                Utils.log Utils.Debug (sprintf "Status db message: %A" msg)
                match msg with
                | SaveStatus(source, status) -> 
                    saveStatuses source [status]
                    return! loop()
                | SaveStatuses(source, statuses) -> 
                    saveStatuses source statuses
                    return! loop()
                | DeleteStatus(status) -> 
                    deleteStatus status
                    return! loop()
                | LoadStatuses(chnl) -> 
                    chnl.Reply(loadStatuses())
                    return! loop()
                | GetLastTwitterStatusId(chnl) ->
                    chnl.Reply(getLastTwitterStatusId())
                    return! loop()
                | UpdateLastTwitterStatusId(status) ->
                    updateLastTwitterStatusId status
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
                    return! loop()}
            Utils.log Utils.Debug "Starting status db"
            loop()
        )
    member x.SaveStatus(source, status) = mbox.Post(SaveStatus(source, status))
    member x.SaveStatuses(source, statuses) = mbox.Post(SaveStatuses(source, statuses))
    member x.DeleteStatus(status) = mbox.Post(DeleteStatus(status))
    member x.LoadStatuses() = mbox.PostAndReply(LoadStatuses)
    member x.GetLastTwitterStatusId() = mbox.PostAndReply(GetLastTwitterStatusId)
    member x.UpdateLastTwitterStatusId(status:status) = mbox.Post(UpdateLastTwitterStatusId(status))
    member x.ReadStatusWithId(id:Int64) = mbox.PostAndReply(fun reply -> ReadStatusWithId(id, reply))
    member x.ReadStatusReplies(id:Int64) = mbox.PostAndReply(fun reply -> ReadStatusReplies(id, reply))
    member x.GetRootStatusesHavingReplies(maxCount) = mbox.PostAndReply(fun reply -> GetRootStatusesHavingReplies(maxCount, reply))
    member x.GetTimelineStatusesBefore(count:int, fromId:Int64) = mbox.PostAndReply(fun reply -> GetTimelineStatusesBefore(count, fromId, reply))
    member x.GetStatusesFromSql(sql) = mbox.PostAndReply(fun reply -> GetStatusesFromSql(sql, reply))

    member x.AsyncLoadStatuses() = mbox.PostAndAsyncReply(LoadStatuses)
    member x.AsyncGetLastTwitterStatusId() = mbox.PostAndAsyncReply(GetLastTwitterStatusId)
    member x.AsyncReadStatusWithId(id:Int64) = mbox.PostAndAsyncReply(fun reply -> ReadStatusWithId(id, reply))
    member x.AsyncReadStatusReplies(id:Int64) = mbox.PostAndAsyncReply(fun reply -> ReadStatusReplies(id, reply))
    member x.AsyncGetRootStatusesHavingReplies(maxCount) = mbox.PostAndAsyncReply(fun reply -> GetRootStatusesHavingReplies(maxCount, reply))
    member x.AsyncGetTimelineStatusesBefore(count, fromId) = mbox.PostAndAsyncReply(fun reply -> GetTimelineStatusesBefore(count, fromId, reply))

let statusesDb = new StatusesDbState()