module test.urlShortening

open NUnit.Framework
open FsUnit
open UrlShortener


[<TestFixture>] 
type ``Given url shortened links`` () =

    [<Test>]
    member test.``Test bitly link`` () =
      extract "http://bit.ly/nLwBIq" 
      |> should equal "http://blogs.msdn.com/b/powershell/archive/2011/08/02/extending-discounted-registration-amp-session-proposal-deadline.aspx"
      
    [<Test>]
    member test.``Test bitly link2`` () =
      extract "http://bit.ly/p6sA3V" 
      |> should equal "http://www.ted.com/talks/kevin_slavin_how_algorithms_shape_our_world.html"
      
    [<Test>]
    member test.``Test tco link`` () =
      extract "http://t.co/YuhrqWU" 
      |> should equal "http://blogs.technet.com/b/markrussinovich/archive/2011/08/02/3442328.aspx"
      
    [<Test>]
    [<Ignore("Failing because of bad encoding")>]
    member test.``Test tco link2`` () =
      extract "http://t.co/vSqeouT" 
      |> should equal "http://translate.google.com/#en|en|Jak startuje stará škodovka: vvvvvvvvvvvvvvvvvvvvvvvvvvvv. vvvvvvvvvvvvvvvvvvvvvv. vvvvvv. vvvvvv"
      
    [<Test>]
    member test.``Test googl link`` () =
      extract "http://goo.gl/KhfKc" 
      |> should equal "https://sites.google.com/site/opencallforgdd/the-challenge-1"
      
    [<Test>]
    member test.``Test googl link2`` () =
      extract "http://goo.gl/LN6Y" 
      |> should equal "https://www.google.com/voice/b/0/rates"