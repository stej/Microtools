module test.statusParsing

open NUnit.Framework
open FsUnit
open System.Xml
open System.IO
open Utils
open Status
open OAuthFunctions

(*
type ``Given status xml document`` ()=
    let xml = new XmlDocument()
    do
      let path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "test\\testStatus.xml")
      printfn "%s" path
      xml.Load(path)
      

    [<Test>] member test.
      ``parse status`` () =
            let s = xml2Status xml
            s.Id |> should equal 4L
*)
[<TestFixture>] 
type Givenstatusxmldocument ()=
    let xml = new XmlDocument()
    do
      let path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "test\\testStatus.xml")
      printfn "%s" path
      xml.Load(path)

    [<Test>] 
    member test.parsestatus () =
        let s = xml2Status (xml.SelectSingleNode("status"))
        //printfn "status %A" s
        s |> should not (equal None)
        s.Value.StatusId |> should equal 119519838443024384L
        s.Value.Text     |> should equal "It's funny but I've found that the people who annoy me the most at first often turn out to be the best friends in the long term"
        s.Value.UserName |> should equal "rickasaurus"
        s.Value.Date     |> should equal (System.DateTime.Parse("2011-09-29 21:12:04"))
        s.Value.UserFavoritesCount |> should equal 254
        s.Value.UserStatusesCount |> should equal 18763
        
[<TestFixture>] 
type Givenretweetxmldocument ()=
    let xml = new XmlDocument()
    do
      let path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "test\\testRetweet.xml")
      printfn "%s" path
      xml.Load(path)

    [<Test>] 
    member test.parseretweet () =
        let s = xml2Retweet (xml.SelectSingleNode("status"))
        //printfn "status %A" s
        s |> should not (equal None)
        s.Value.StatusId |> should equal 119514078736695298L
        s.Value.Text     |> should equal "Happening now - September 29th the Streaming API is turning SSL only - http://t.co/mlBeUUSQ"
        s.Value.UserName |> should equal "sitestreams"
        s.Value.Date     |> should equal (System.DateTime.Parse("2011-09-29 20:49:11"))
        s.Value.UserFavoritesCount |> should equal 0
        s.Value.UserStatusesCount |> should equal 150
        
        s.Value.RetweetInfo |> should not (equal None)
        let retweet = s.Value.RetweetInfo.Value
        retweet.UserName           |> should equal "twitterapi"
        retweet.UserFavoritesCount |> should equal 22
        retweet.UserStatusesCount  |> should equal 3119