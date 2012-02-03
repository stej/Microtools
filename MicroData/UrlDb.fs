module UrlDb

open System
open System.Data.SQLite
open Utils
open DbCommon
open ShortenerDbInterface

let mutable fileName = "urls.db"

let private readUrl (rd:SQLiteDataReader) =
    { ShortUrl = str rd "ShortUrl"
      LongUrl  = str rd "LongUrl"
      Date     = date rd "Date"
      StatusId = long rd "StatusId"
      Complete = bol rd "Complete"
    }

type private UrlsDbMessages =
| TranslateUrl of string * AsyncReplyChannel<ShortUrlInfo option>
| SaveUrl of ShortUrlInfo
| GetUrlsFromSql of string * AsyncReplyChannel<ShortUrlInfo list>
| SetComplete of string 
| UpdateExtracted of string * string

type UrlsDbState(file) =
    let translateUrl (shortUrl:string) = 
        printfn "Translate %s" shortUrl
        useDb file (fun conn ->
            let query = "select * from UrlTranslation where ShortUrl = @p0"
            use cmd = conn.CreateCommand(CommandText = query)
            cmd.Parameters.Add(new SQLiteParameter("@p0", shortUrl)) |> ignore
            let rd = cmd.ExecuteReader()
            let ret = 
                if rd.Read() then
                    Some(readUrl rd)
                else
                    None
            rd.Close()
            ret
        )
    let saveUrl (urlInfo:ShortUrlInfo) = 
        printfn "Store %s->%s (%d) at %A" urlInfo.ShortUrl urlInfo.LongUrl urlInfo.StatusId urlInfo.Date
        useDb file (fun conn ->
            use cmd = conn.CreateCommand(CommandText = 
                "INSERT INTO UrlTranslation(ShortUrl, LongUrl, Date, StatusId, Complete) VALUES(@p0, @p1, @p2, @p3, @p4)"
            )
            addCmdParameter cmd "@p0" urlInfo.ShortUrl
            addCmdParameter cmd "@p1" urlInfo.LongUrl
            addCmdParameter cmd "@p2" urlInfo.Date.Ticks
            addCmdParameter cmd "@p3" urlInfo.StatusId
            addCmdParameter cmd "@p4" urlInfo.Complete
            cmd.ExecuteNonQuery() |> ignore
        )
    let getUrlsFromSql sql =
        ldbgp "getUrlsFromSql {0}" sql
        useDb file (fun conn ->
            use cmd = conn.CreateCommand()
            cmd.CommandText <- sql
            executeSelect readUrl cmd
        )

    let setComplete shortUrl =
        printfn "Update complete %s " shortUrl
        useDb file (fun conn ->
            use cmd = conn.CreateCommand(CommandText = "UPDATE UrlTranslation SET Complete = 1 where ShortUrl = @p0")
            addCmdParameter cmd "@p0" shortUrl
            cmd.ExecuteNonQuery() |> ignore
        )
    let updateExtracted shortUrl longUrl =
        printfn "Update extracted %s / %s" shortUrl longUrl
        useDb file (fun conn ->
            use cmd = conn.CreateCommand(CommandText = 
                        "UPDATE UrlTranslation SET Complete = 1, LongUrl = @p0 where ShortUrl = @p1")
            addCmdParameter cmd "@p0" longUrl
            addCmdParameter cmd "@p1" shortUrl
            cmd.ExecuteNonQuery() |> ignore
        )
    let mbox = MailboxProcessor.Start(fun mbox ->
            printfn "starting urls db"
            let rec loop() = async {
                let! msg = mbox.Receive()
                ldbgp "Url db message: {0}" msg
                match msg with
                | TranslateUrl(url, chnl) -> 
                    chnl.Reply(translateUrl url)
                    return! loop()
                | SaveUrl(urlInfo) -> 
                    saveUrl urlInfo
                    return! loop()
                | GetUrlsFromSql(sql, chnl) ->
                    chnl.Reply(getUrlsFromSql(sql))
                    return! loop()
                | SetComplete(shortUrl) ->
                    setComplete shortUrl
                    return! loop()
                | UpdateExtracted(shortUrl, longUrl) ->
                    updateExtracted shortUrl longUrl
                    return! loop()
            }
            ldbg "Starting url db"
            loop()
        )
    do
        mbox.Error.Add(fun exn -> printfn "exception: %A" exn
                                  lerrex exn "Error in url db mailbox")

    member x.GetUrlsFromSql(sql) = mbox.PostAndReply(fun reply -> GetUrlsFromSql(sql, reply))

    interface ShortenerDbInterface.IShortUrlsDatabase with
        member x.TranslateUrl(shortUrl) = mbox.PostAndReply(fun reply -> TranslateUrl(shortUrl, reply))
        member x.SaveUrl(info) = mbox.Post(SaveUrl(info))
        member x.SaveIncompleteUrl(info) = mbox.Post(SaveUrl({info with Complete = false}))
        member x.SetComplete(shortUrl) = mbox.Post(SetComplete(shortUrl))
        member x.UpdateExtracted(shortUrl, longUrl) = mbox.Post(UpdateExtracted(shortUrl, longUrl))

let urlsDb = new UrlsDbState(fileName)