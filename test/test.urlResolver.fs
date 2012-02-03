module test.urlResolver

open NUnit.Framework
open FsUnit
open UrlResolver
open testDbHelpers.testUrlsDbUtils

[<TestFixture>] 
type ``Given url resolver capable to extract url or load from url db`` () =

    let db = dbInterface()

    let saveUrl short long complete =
        db.SaveUrl({ ShortUrl = short
                     LongUrl  = long
                     Date     = System.DateTime.Now
                     StatusId = 15L
                     Complete = complete})
    let getInfo short =
        db.TranslateUrl(short)

    [<Test>]
    member test.``If record is already extracted, resolver returns extracted`` () =
      printfn "Deleted rows: %A" (deleteDbContent())
      saveUrl "http://t.co/abc" "http://www.google.com" false

      let resolver = new UrlResolver(db)
      let info = Async.RunSynchronously (async { return! resolver.AsyncResolveUrl("http://t.co/abc", 15L) })
      info |> should equal "http://www.google.com"

    [<Test>]
    member test.``If record is already extracted but not Complete, resolver marks it complete`` () =
      printfn "Deleted rows: %A" (deleteDbContent())
      saveUrl "http://t.co/abc" "http://www.google.com" false

      let resolver = new UrlResolver(db)
      Async.RunSynchronously (async { return! resolver.AsyncResolveUrl("http://t.co/abc", 15L) }) |> ignore
      let info = getInfo "http://t.co/abc"
      info.Value.LongUrl |> should equal "http://www.google.com"
      info.Value.Complete |> should equal true

    [<Test>]
    member test.``If record is not in db, resolver creates a new complete one`` () =
      printfn "Deleted rows: %A" (deleteDbContent())

      let resolver = new UrlResolver(db)
      Async.RunSynchronously (async { return! resolver.AsyncResolveUrl("http://t.co/YuhrqWU", 5L) }) |> ignore
      let info = getInfo "http://t.co/YuhrqWU"
      info.Value.ShortUrl |> should equal "http://t.co/YuhrqWU"
      info.Value.LongUrl |> should equal "http://blogs.technet.com/b/markrussinovich/archive/2011/08/02/3442328.aspx"
      info.Value.Complete |> should equal true
      info.Value.StatusId |> should equal 5L

    [<Test>]
    member test.``If there is record with possible shortened url, resolver expands it and marks as complete`` () =
      printfn "Deleted rows: %A" (deleteDbContent())
      saveUrl "http://t.co/YuhrqWU" "http://bit.ly/oTMMi6" false

      let resolver = new UrlResolver(db)
      Async.RunSynchronously (async { return! resolver.AsyncResolveUrl("http://t.co/YuhrqWU", 15L) }) |> ignore

      let info = getInfo "http://t.co/YuhrqWU"
      info.Value.ShortUrl |> should equal "http://t.co/YuhrqWU"
      info.Value.LongUrl |> should equal "http://blogs.technet.com/b/markrussinovich/archive/2011/08/02/3442328.aspx"
      info.Value.Complete |> should equal true
      info.Value.StatusId |> should equal 15L

    (*[<Test>]
    member test.``Test bitly link2`` () =
      extract "http://bit.ly/p6sA3V" 
      |> should equal (Extracted("http://www.ted.com/talks/kevin_slavin_how_algorithms_shape_our_world.html"))
      
    [<Test>]
    member test.``Test tco link`` () =
      extract "http://t.co/YuhrqWU" 
      |> should equal (Extracted("http://blogs.technet.com/b/markrussinovich/archive/2011/08/02/3442328.aspx"))
      
    [<Test>]
    [<Ignore("Failing because of bad encoding")>]
    member test.``Test tco link2`` () =
      extract "http://t.co/vSqeouT" 
      |> should equal (Extracted("http://translate.google.com/#en|en|Jak startuje stará škodovka: vvvvvvvvvvvvvvvvvvvvvvvvvvvv. vvvvvvvvvvvvvvvvvvvvvv. vvvvvv. vvvvvv"))
      
    [<Test>]
    member test.``Test googl link`` () =
      extract "http://goo.gl/KhfKc" 
      |> should equal (Extracted("https://sites.google.com/site/opencallforgdd/the-challenge-1"))
      
    [<Test>]
    member test.``Test googl link2`` () =
      extract "http://goo.gl/LN6Y" 
      |> should equal (Extracted("https://www.google.com/voice/b/0/rates"))
      
    [<Test>]
    member test.``Test jdem.cz`` () =
      extract "http://jdem.cz/aabi" 
      |> should equal (Extracted("http://www.mapy.cz/#x=130384128@y=132950912@z=13@mm=ZP@sa=s@st=s@ssq=onen%20sv%C4%9Bt@sss=1@ssp=134363136_138017024_134561792_138187264"))
      *)