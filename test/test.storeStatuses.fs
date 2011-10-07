module test.storeStatuses

open NUnit.Framework
open FsUnit
open System.Xml
open System.IO
open Utils
open Status
open OAuthFunctions
open StatusDb
open testDbUtils

[<TestFixture>] 
type ``Given empty database`` () =

    [<Test>] 
    member test.``Store Retweet and check that it exists in db`` () =
        printfn "Store Retweet and check that it exists in db"
        printfn "Deleted rows: %A" (deleteDbContent())
        
        let db = dbInterface()
        db.SaveStatus({Status = retweet; Children = new  ResizeArray<statusInfo>(); Source = Timeline})
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