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
    }

type private UrlsDbMessages =
| TranslateUrl of string * AsyncReplyChannel<ShortUrlInfo option>
| SaveUrl of ShortUrlInfo

type UrlsDbState(file) =
    let translateUrl (shortUrl:string) = 
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
        useDb file (fun conn ->
            use cmd = conn.CreateCommand(CommandText = 
                "INSERT INTO UrlTranslation(ShortUrl, LongUrl, Date, StatusId) VALUES(@p0, @p1, @p2, @p3)"
            )
            addCmdParameter cmd "@p0" urlInfo.ShortUrl
            addCmdParameter cmd "@p1" urlInfo.LongUrl
            addCmdParameter cmd "@p2" urlInfo.Date
            addCmdParameter cmd "@p3" urlInfo.StatusId
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
            }
            ldbg "Starting url db"
            loop()
        )
    do
        mbox.Error.Add(fun exn -> printfn "exception: %A" exn
                                  lerrex exn "Error in url db mailbox")

    interface ShortenerDbInterface.IShortUrlsDatabase with
        member x.TranslateUrl(shortUrl) = mbox.PostAndReply(fun reply -> TranslateUrl(shortUrl, reply))
        member x.SaveUrl(shortUrl) = mbox.Post(SaveUrl(shortUrl))

let urlsDb = new UrlsDbState(fileName)