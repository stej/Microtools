module test.statusesChecker

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
type ``Given statuses checker`` () =

    let statusPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "test\\testStatus.xml")
    
    let setupOAuthInterface check register request =
      OAuthInterface.oAuthAccess <- { 
        new OAuthInterface.IOAuth with
          member x.checkAccessTokenFile() = match check with | None -> OAuth.checkAccessTokenFile() | Some(f) -> f()
          member x.registerOnTwitter() = match register  with | None -> OAuth.registerOnTwitter() | Some(f) -> f()
          member x.requestTwitter(url) = match request with | None -> OAuth.requestTwitter(url) | Some(f) -> f(url)
      }
    let getDummyStatusInfo _ =
      Status.status2StatusInfo Timeline None
    [<Test>] 
    member test.``When can not query, None is returned`` () =
        printfn "When can not query, None is returned-------------------------------------------------------"
        let c = new TwitterStatusesChecker.Checker(Twitter.FriendsStatuses, getDummyStatusInfo, (fun () -> ""), (fun () -> false))
        let res = async { return! c.Check() } |> Async.RunSynchronously
        printfn "%A" res
        match res with
        | None -> ()
        | _ -> Assert.Fail()
    
    [<Test>] 
    member test.``When None is returned from OAuth, None is returned from checker`` () =
        printfn "When None is returned from OAuth, None is returned from checker-------------------------------------------------------"
        
        setupOAuthInterface None None (Some(fun _ ->
          None
        ))
        let c = new TwitterStatusesChecker.Checker(Twitter.FriendsStatuses, getDummyStatusInfo, (fun () -> ""), (fun () -> true))
        let res = async { return! c.Check() } |> Async.RunSynchronously
        printfn "%A" res
        match res with
        | None -> ()
        | Some(s) -> Assert.Fail()
        
    [<Test>] 
    member test.``When proper request is done and status from Twitter returned, it should be returned from Check() method`` () =
        printfn "When exception is thrown, None is returned-------------------------------------------------------"
        
        setupOAuthInterface None None (Some(fun _ ->
          ("<statuses>"+System.IO.File.ReadAllText(statusPath)+"</statuses>",
           System.Net.HttpStatusCode.OK,
           new System.Net.WebHeaderCollection())
          |> Some
        ))
        let c = new TwitterStatusesChecker.Checker(Twitter.FriendsStatuses, getDummyStatusInfo, (fun () -> ""), (fun () -> true))
        let res = async { return! c.Check() } |> Async.RunSynchronously
        printfn "%A" res
        match res with
        | None -> Assert.Fail()
        | Some(s) -> ()
        
    [<Test>] 
    member test.``When proper request is done and concrete status from Twitter returned, Check should return the same status`` () =
        printfn "When exception is thrown, None is returned-------------------------------------------------------"
        
        setupOAuthInterface None None (Some(fun _ ->
          ("<statuses>"+System.IO.File.ReadAllText(statusPath)+"</statuses>",
           System.Net.HttpStatusCode.OK,
           new System.Net.WebHeaderCollection())
          |> Some
        ))
        let c = new TwitterStatusesChecker.Checker(Twitter.FriendsStatuses, (OAuthFunctions.xml2Status >> (status2StatusInfo Timeline)), (fun () -> ""), (fun () -> true))
        let res = async { return! c.Check() } |> Async.RunSynchronously
        printfn "%A" res
        match res with
        | Some([s]) -> s.Status.StatusId |> should equal 119519838443024384L 
                       s.Status.Text     |> should equal "It's funny but I've found that the people who annoy me the most at first often turn out to be the best friends in the long term"
                       s.Source          |> should equal Timeline
        | _         -> Assert.Fail()