namespace testDbHelpers 

open System.Xml
open System.IO
open StatusXmlProcessors
open StatusDb
open UrlDb
open DbCommon

module testStatusesDbUtils =

    let dbPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "statuses.db")
    let retweet =
        let path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "testRetweet.xml")
        printfn "%s" path
        let xml = new XmlDocument()
        xml.Load(path)
        (xml2Retweet (xml.SelectSingleNode("status"))).Value

    let getDbObject() = 
        new StatusesDbState(dbPath)
    let dbInterface() =
        getDbObject() :> DbInterface.IStatusesDatabase
    let deleteDbContent() =
        useDb dbPath (fun conn ->
            use cmd = conn.CreateCommand(CommandText = "delete from Status; delete from RetweetInfo")
            cmd.ExecuteNonQuery()
)

module testUrlsDbUtils =

    open StatusDb
    open DbCommon

    let dbPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "urls.db")

    let getDbObject() = 
        printfn "Returning new urls db state, path: %s" dbPath
        new UrlsDbState(dbPath)
    let dbInterface() =
        getDbObject() :> ShortenerDbInterface.IShortUrlsDatabase
    let deleteDbContent() =
        useDb dbPath (fun conn ->
            use cmd = conn.CreateCommand(CommandText = "delete from UrlTranslation;")
            cmd.ExecuteNonQuery()
        )