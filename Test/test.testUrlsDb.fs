module test.testUrlsDb

open NUnit.Framework
open FsUnit
open System
open System.Xml
open System.IO
open Utils
open Status
open StatusXmlProcessors
open StatusDb
open testDbHelpers.testUrlsDbUtils

[<TestFixture>] 
type ``Given empty urls database`` () =

    [<Test>] 
    member test.``Response for translation in empty db should return None`` () =
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        db.TranslateUrl("any").IsNone |> should be True

    [<Test>] 
    member test.``Response for translation for given url when there is only other record returns None `` () =
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        db.SaveUrl({ ShortUrl = "a"
                     LongUrl  = "b"
                     Date     = DateTime.Now
                     StatusId = 1L})
        db.TranslateUrl("b").IsNone |> should be True

    [<Test>] 
    member test.``Response for translation for given url when there is record returns correct translation`` () =
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        let now = DateTime.Now
        db.SaveUrl({ ShortUrl = "a"
                     LongUrl  = "b"
                     Date     = now;
                     StatusId = 5L })

        let record = db.TranslateUrl("a")
        record.IsSome |> should be True
        record.Value.ShortUrl |> should equal "a"
        record.Value.LongUrl |> should equal "b"
        record.Value.Date |> should equal now
        record.Value.StatusId |> should equal 5L

    [<Test>] 
    member test.``Store many urls infos and check that all are stored and have correct translations`` () =
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        let now = DateTime.Now
        [1..100] 
        |> List.map (fun i -> async {  db.SaveUrl({ ShortUrl = ("a"+i.ToString())
                                                    LongUrl  = ("b"+i.ToString())
                                                    Date     = now;
                                                    StatusId = 5L }) |> ignore })
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore

        [100..-1..1] 
        |> List.iter (fun i -> 
            let record = db.TranslateUrl("a"+i.ToString())
            record.IsSome |> should be True
            record.Value.ShortUrl |> should equal ("a"+i.ToString())
            record.Value.LongUrl |> should equal ("b"+i.ToString())
            record.Value.Date |> should equal now
            record.Value.StatusId |> should equal 5L
        )

    (*[<Test>] 
    member test.``Store short url translation`` () =
        printfn "Store url translation and check that it exists in db"
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        db.TranslateUrl({})
        let readStatus = db.ReadStatusWithId(retweet.StatusId)
        
        readStatus.IsSome |> should be True
        readStatus.Value.Status.Text |> should equal "Happening now - September 29th the Streaming API is turning SSL only - http://t.co/mlBeUUSQ"
        
    [<Test>] 
    member test.``Store status from search then from timeline then should be stored as from timeline`` () =
        printfn "Store status from search then from timeline then should be stored as from timeline"
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        db.SaveStatus({Status = retweet; Children = new  ResizeArray<statusInfo>(); Source = RequestedConversation})
        db.SaveStatus({Status = retweet; Children = new  ResizeArray<statusInfo>(); Source = Timeline})
        let readStatus = db.ReadStatusWithId(retweet.StatusId)
        
        match readStatus.Value.Source with 
        | Timeline -> ()
        | _ -> Assert.Fail()
        
    [<Test>] 
    member test.``Store status from timeline then from search then should be stored as from timeline``  () =
        printfn "Store status from timeline then from search then should be stored as from timeline"
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        db.SaveStatus({Status = retweet; Children = new  ResizeArray<statusInfo>(); Source = Timeline})
        db.SaveStatus({Status = retweet; Children = new  ResizeArray<statusInfo>(); Source = RequestedConversation})
        let readStatus = db.ReadStatusWithId(retweet.StatusId)
        
        match readStatus.Value.Source with 
        | Timeline -> ()
        | _ -> Assert.Fail()
        
    [<Test>] 
    member test.``Store status without retweet info and then store later with retweet info. The retweet info should be saved`` () =
        printfn "Store status without retweet info and then store later with retweet info. The retweet info should be saved"
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        db.SaveStatus({Status = { retweet with RetweetInfo = None}; Children = new  ResizeArray<statusInfo>(); Source = Timeline})
        db.SaveStatus({Status = retweet; Children = new ResizeArray<statusInfo>(); Source = RequestedConversation})
        
        let readStatus = db.ReadStatusWithId(retweet.StatusId)
        let retweetInfo = readStatus.Value.Status.RetweetInfo
        retweetInfo.IsSome |> should be True
        retweetInfo.Value.RetweetId |> should equal 119516516159991810L
        retweetInfo.Value.UserName |> should equal "twitterapi"

*)