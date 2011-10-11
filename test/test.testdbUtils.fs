module testDbUtils

open NUnit.Framework
open FsUnit
open System.Xml
open System.IO
open Utils
open Status
open OAuthFunctions
open StatusDb

let dbPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "test\\statuses.db")
let retweet =
    let path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "test\\testRetweet.xml")
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