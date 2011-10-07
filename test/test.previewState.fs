module test.previewState

open NUnit.Framework
open FsUnit
open System.IO
open Utils
open Status
open PreviewsState

[<TestFixture>] 
type ``Given object that holds preview state`` ()=
    let newStatus id replyId text = 
      let s = getEmptyStatus()
      { Status = { s with StatusId = id; ReplyTo = replyId; Text = text }
        Children = new ResizeArray<statusInfo>()
        Source = Timeline }
    let newRetweet id replyid text retweetId =
      (*let s = getEmptyStatus()
      { Status = { s with StatusId = id; ReplyTo = replyId; RetweetInfo = { getEmptyRetweetInfo() with RetweetId = retweetId } }
        Children = new ResizeArray<statusInfo>()
        Source = Timeline }
      *)
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
    let dumpTrees trees = 
      let rec dumpTree depth tree =
        let s = tree.Status
        printf "%s" (new System.String(' ', depth*2))
        printfn "%A%s %s" s.StatusId (if s.RetweetInfo.IsSome then "(R)" else "") s.Text
        tree.Children |> Seq.iter (dumpTree (depth+1))
      trees |> List.iter (dumpTree 0)

    [<Test>]
    member test.``Initialy the previews state is empty`` () =
        printfn "Initialy the previews state is empty"
        let previewCache = new UserStatusesState()
        
        let statusesList, statusesTree = previewCache.GetStatuses()
        statusesList.Length |> should equal 0
        statusesTree.Length |> should equal 0
    
    [<Test>]
    member test.``One status added should be returned in both lists - list & rooted`` () =
        printfn "One status added should be returned in both lists - list & rooted"
        let previewCache = new UserStatusesState()
        
        [newStatus 1L -1L "hi there"] |> previewCache.AddStatuses
        
        let statusesList, statusesTree = previewCache.GetStatuses()
        statusesList.Length |> should equal 1
        statusesTree.Length |> should equal 1
        
        statusesList |> statusIdAtIndex 0 |> should equal 1L
        statusesTree |> statusIdAtIndex 0 |> should equal 1L
        
    [<Test>]
    member test.``Added status and descendant later should be added to tree properly`` () =
        printfn "Added status and descendant later should be added to tree properly"
        let previewCache = new UserStatusesState()
        
        [newStatus 1L -1L "how many?"] |> previewCache.AddStatuses
        [newStatus 2L 1L "only two"] |> previewCache.AddStatuses
        
        let statusesList, statusesTree = previewCache.GetStatuses()
        statusesList.Length |> should equal 2
        statusesTree.Length |> should equal 1
        
        statusesList |> listContainsStatusWithId 1L |> should be True
        statusesList |> listContainsStatusWithId 2L |> should be True
        statusesTree |> listContainsStatusWithId 1L |> should be True
        statusesTree |> listContainsStatusWithId 2L |> should be False
        
        statusesTree.[0].Children.Count                 |> should equal 1 
        statusesTree.[0] |> nthChild [0] |> getStatusId |> should equal 2
        
    [<Test>]
    member test.``Added more statuses and then more descendants one by one should be added to tree properly`` () =
        printfn "Added more statuses and then more descendants one by one should be added to tree properly"
        let previewCache = new UserStatusesState()
        
        [newStatus 1L -1L "like Twitter?"] |> previewCache.AddStatuses
        [newStatus 2L 1L  "of course! you?"]  |> previewCache.AddStatuses
        
        [newStatus 20L -1L "how many people are there?"]  |> previewCache.AddStatuses
        [newRetweet 21L 20L "do you really need to know it?" 121L]  |> previewCache.AddStatuses
        [newStatus 22L 20L "I guess only a few"]  |> previewCache.AddStatuses
        [newStatus 23L 22L "a few.. two?"]  |> previewCache.AddStatuses
        [newStatus 24L 21L "yep, I do!"]  |> previewCache.AddStatuses
        [newStatus 25L 24L "ok, then just two"]  |> previewCache.AddStatuses
        
        // later reply
        [newStatus 3L 2L "sure"]  |> previewCache.AddStatuses
        // later added ordinary status (no retweet)
        [newStatus 21L 20L "do you really need to know it?"]  |> previewCache.AddStatuses
        // retweet 
        [newRetweet 24L 21L "yep, I do!" 124L]  |> previewCache.AddStatuses
        
        let statusesList, statusesTree = previewCache.GetStatuses()
        let tree = statusesTree |> List.sortBy (fun sInfo -> sInfo.Status.StatusId)
        
        printfn "dump:"
        dumpTrees tree
        
        statusesList.Length |> should equal 9
        statusesTree.Length |> should equal 2
        
        statusesTree |> listContainsStatusWithId 1L  |> should be True
        statusesTree |> listContainsStatusWithId 20L |> should be True

        tree.[0]                   |> getStatusId |> should equal 1
        tree.[0] |> nthChild [0]   |> getStatusId |> should equal 2
        tree.[0] |> nthChild [0;0] |> getStatusId |> should equal 3
        
        tree.[1]                   |> getStatusId |> should equal 20
        tree.[1] |> nthChild [0]   |> getStatusId |> should equal 21
        tree.[1] |> nthChild [1]   |> getStatusId |> should equal 22
        tree.[1] |> nthChild [1;0] |> getStatusId |> should equal 23
        tree.[1] |> nthChild [0;0] |> getStatusId |> should equal 24
        tree.[1] |> nthChild [0;0;0] |> getStatusId |> should equal 25
        
        (tree.[1] |> nthChild [0;0]).Status.RetweetInfo.IsSome |> should be True
        (tree.[1] |> nthChild [0]).Status.RetweetInfo.IsSome |> should be True
        
    [<Test>]
    member test.``Added more statuses and more descenants at the same time should be added to tree properly`` () =
        printfn "Added more statuses and more descenants at the same time should be added to tree properly"
        let previewCache = new UserStatusesState()
        
        [newStatus 1L -1L   "like Twitter?"
         newStatus 2L 1L    "of course! you?"
         newStatus 20L -1L  "how many people are there?"
         newRetweet 21L 20L "do you really need to know it?" 121L
         newStatus 22L 20L  "I guess only a few"
         newStatus 23L 22L  "a few.. two?"
         newStatus 24L 21L  "yep, I do!"
         newStatus 25L 24L  "ok, then just two"
         newStatus 3L 2L    "sure"
         newStatus 21L 20L  "do you really need to know it?"
         newRetweet 24L 21L "yep, I do!" 124L
        ]
        |> previewCache.AddStatuses
        
        let statusesList, statusesTree = previewCache.GetStatuses()
        let tree = statusesTree |> List.sortBy (fun sInfo -> sInfo.Status.StatusId)
        
        printfn "dump:"
        dumpTrees tree
        
        statusesList.Length |> should equal 9
        statusesTree.Length |> should equal 2
        
        statusesTree |> listContainsStatusWithId 1L  |> should be True
        statusesTree |> listContainsStatusWithId 20L |> should be True

        tree.[0]                   |> getStatusId |> should equal 1
        tree.[0] |> nthChild [0]   |> getStatusId |> should equal 2
        tree.[0] |> nthChild [0;0] |> getStatusId |> should equal 3
        
        tree.[1]                   |> getStatusId |> should equal 20
        tree.[1] |> nthChild [0]   |> getStatusId |> should equal 21
        tree.[1] |> nthChild [1]   |> getStatusId |> should equal 22
        tree.[1] |> nthChild [1;0] |> getStatusId |> should equal 23
        tree.[1] |> nthChild [0;0] |> getStatusId |> should equal 24
        tree.[1] |> nthChild [0;0;0] |> getStatusId |> should equal 25
        
        (tree.[1] |> nthChild [0;0]).Status.RetweetInfo.IsSome |> should be True
        (tree.[1] |> nthChild [0]).Status.RetweetInfo.IsSome |> should be True

    [<Test>]
    member test.``When adding status and then retweet, it should be merged`` () =
        printfn "When adding status and then retweet, it should be merged"
        let previewCache = new UserStatusesState()
        
        [newStatus 1L -1L "some status"] |> previewCache.AddStatuses
        [newRetweet 1L -1L "some retweet" 2L]  |> previewCache.AddStatuses
        
        let statusesList, statusesTree = previewCache.GetStatuses()
        statusesList.Length |> should equal 1
        statusesTree.Length |> should equal 1
        
        statusesList.[0].Status.RetweetInfo.IsSome |> should be True
        statusesList.[0].Status.RetweetInfo.Value.RetweetId |> should equal 2L