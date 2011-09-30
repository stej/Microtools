module test.storeStatuses

open NUnit.Framework
open FsUnit
open System.Xml
open System.IO
open Utils
open Status
open OAuthFunctions
open StatusDb

[<TestFixture>] 
type Givenemptydatabase ()=
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
        getDbObject() :> DbFunctions.IStatusesDatabase
    let deleteDbContent() =
        useDb dbPath (fun conn ->
            use cmd = conn.CreateCommand(CommandText = "delete from Status; delete from RetweetInfo")
            cmd.ExecuteNonQuery()
        )

    [<Test>] 
    member test.StoreRetweetAndCheckThatItExistsInDb () =
        printfn "StoreRetweetAndCheckThatItExistsInDb"
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        db.SaveStatus({Status = retweet; Children = new  ResizeArray<statusInfo>(); Source = Timeline})
        let readStatus = db.ReadStatusWithId(retweet.StatusId)
        
        readStatus.IsSome |> should be True
        readStatus.Value.Status.Text |> should equal "Happening now - September 29th the Streaming API is turning SSL only - http://t.co/mlBeUUSQ"
        
    [<Test>] 
    member test.StoreStatusAsSearchThenAsTimelineThenShouldBeStoredAsTimeline () =
        printfn "StoreStatusAsSearchThenAsTimelineThenShouldBeStoredAsTimeline"
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        db.SaveStatus({Status = retweet; Children = new  ResizeArray<statusInfo>(); Source = RequestedConversation})
        db.SaveStatus({Status = retweet; Children = new  ResizeArray<statusInfo>(); Source = Timeline})
        let readStatus = db.ReadStatusWithId(retweet.StatusId)
        
        match readStatus.Value.Source with 
        | Timeline -> ()
        | _ -> Assert.Fail()
        
    [<Test>] 
    member test.StoreStatusAsTimelineThenAsSearchThenShouldBeStoredAsTimeline () =
        printfn "StoreStatusAsTimelineThenAsSearchThenShouldBeStoredAsTimeline"
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        db.SaveStatus({Status = retweet; Children = new  ResizeArray<statusInfo>(); Source = Timeline})
        db.SaveStatus({Status = retweet; Children = new  ResizeArray<statusInfo>(); Source = RequestedConversation})
        let readStatus = db.ReadStatusWithId(retweet.StatusId)
        
        match readStatus.Value.Source with 
        | Timeline -> ()
        | _ -> Assert.Fail()
        
    [<Test>] 
    member test.StoreStatusWithoutRetweetInfoAndStoreLaterWithRetweetInfo_shouldBeSaved () =
        printfn "StoreStatusWithoutRetweetInfoAndStoreLaterWithRetweetInfo_shouldBeSaved"
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        db.SaveStatus({Status = { retweet with RetweetInfo = None}; Children = new  ResizeArray<statusInfo>(); Source = Timeline})
        db.SaveStatus({Status = retweet; Children = new ResizeArray<statusInfo>(); Source = RequestedConversation})
        
        let readStatus = db.ReadStatusWithId(retweet.StatusId)
        let retweetInfo = readStatus.Value.Status.RetweetInfo
        retweetInfo.IsSome |> should be True
        retweetInfo.Value.RetweetId |> should equal 119516516159991810L
        retweetInfo.Value.UserName |> should equal "twitterapi"