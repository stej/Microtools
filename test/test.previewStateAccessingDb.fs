module test.previewStateAccessingDb

open NUnit.Framework
open FsUnit
open System.IO
open Utils
open Status
open PreviewsState
open System.Xml
open OAuthFunctions
open StatusDb
open testDbUtils


[<TestFixture>] 
type ``Given object that holds preview state`` ()=
    let newStatus id replyId text = 
      let s = getEmptyStatus()
      { Status = { s with StatusId = id; ReplyTo = replyId; Text = text }
        Children = new ResizeArray<statusInfo>()
        Source = Timeline }
    let newRetweet id replyid text retweetId =
      let sInfo = newStatus id replyid text
      { sInfo with 
         Status = { sInfo.Status with 
                     RetweetInfo = Some({ getEmptyRetweetInfo() with RetweetId = retweetId }) } }
    let listContainsStatusWithId id list = 
      list |> List.exists (fun status -> status.Status.StatusId = id)
    let statusIdAtIndex index (list: statusInfo list) =
      list.[index].Status.StatusId
    let nthChild (indexes:int list) sInfo =
      let mutable ret = sInfo
      for index in indexes do
        ret <- ret.Children.[index]
      ret
    let getStatusId sInfo =
      sInfo.Status.StatusId
      
    [<Test>]
    member test.``Parents stored in db are read when creating conversation and parents are requested`` () =
        printfn "Parents stored in db are read when creating conversation and parents are requested"
        printfn "Deleted rows: %A" (deleteDbContent())
        let previewCache = new UserStatusesState()
        let db = dbInterface()
        DbFunctions.dbAccess <- db  // todo: remove :(
        
        let s1 = newStatus 1L -1L "I am root"
        let s2 = newStatus 2L 1L "I am envy! :("
        let s3 = newStatus 3L 2L "Want to be root too?"
        
        for s in [s1; s2; s3] do
          ldbgp "Storing status {0}" s.Status
          db.SaveStatus(s)
        db.ReadStatusWithId(1L) |> ignore  // ensure statuses are saved before continuing
        
        [newStatus 4L 3L "Of course!"] |> previewCache.AddStatuses
        
        let statusesList, statusesTree = previewCache.GetStatuses()
        statusesList.Length |> should equal 1
        statusesTree.Length |> should equal 1
        
        statusesTree.[0]                     |> getStatusId |> should equal 1
        statusesTree.[0] |> nthChild [0]     |> getStatusId |> should equal 2
        statusesTree.[0] |> nthChild [0;0]   |> getStatusId |> should equal 3
        statusesTree.[0] |> nthChild [0;0;0] |> getStatusId |> should equal 4