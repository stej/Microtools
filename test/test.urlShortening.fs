module test.urlShortening

open NUnit.Framework
open FsUnit
open UrlShortenerFunctions


[<TestFixture>] 
type ``Given url shortened links`` () =

    [<Test>]
    member test.``Test bitly link`` () =
      extract "http://bit.ly/nLwBIq" 
      |> should equal (Extracted("http://blogs.msdn.com/b/powershell/archive/2011/08/02/extending-discounted-registration-amp-session-proposal-deadline.aspx"))
      
    [<Test>]
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

//[<TestFixture>]
//type Many () =
//    [<Test>]
//    member test.``Test jdem1`` () =
//      printfn "%s -> %A" "http://jdem.cz/sudz6" (extract "http://jdem.cz/sudz6")
//    [<Test>]
//    member test.``Test jdem2`` () =
//      printfn "%s -> %A" "http://jdem.cz/sssc8" (extract "http://jdem.cz/sssc8")
//    [<Test>]
//    member test.``Test jdem3`` () =
//      printfn "%s -> %A" "http://jdem.cz/ssek5" (extract "http://jdem.cz/ssek5")
//    [<Test>]
//    member test.``Test jdem4`` () =
//      printfn "%s -> %A" "http://jdem.cz/ssr85" (extract "http://jdem.cz/ssr85")
//    [<Test>]
//    member test.``Test jdem5`` () =
//      printfn "%s -> %A" "http://jdem.cz/sssa8" (extract "http://jdem.cz/sssa8")
//    [<Test>]
//    member test.``Test jdem6`` () =
//      printfn "%s -> %A" "http://jdem.cz/sse38" (extract "http://jdem.cz/sse38")
//    [<Test>]
//    member test.``Test jdem7`` () =
//      printfn "%s -> %A" "http://jdem.cz/ssch8" (extract "http://jdem.cz/ssch8")
//    [<Test>]
//    member test.``Test jdem8`` () =
//      printfn "%s -> %A" "http://jdem.cz/ssgh3" (extract "http://jdem.cz/ssgh3")
//    [<Test>]
//    member test.``Test jdem9`` () =
//      printfn "%s -> %A" "http://jdem.cz/ssgk4" (extract "http://jdem.cz/ssgk4")
//    [<Test>]
//    member test.``Test jdem10`` () =
//      printfn "%s -> %A" "http://jdem.cz/ssgm4" (extract "http://jdem.cz/ssgm4")
//    [<Test>]
//    member test.``Test jdem11`` () =
//      printfn "%s -> %A" "http://jdem.cz/ssgq7" (extract "http://jdem.cz/ssgq7")
//    [<Test>]
//    member test.``Test jdem12`` () =
//      printfn "%s -> %A" "http://jdem.cz/ssk92" (extract "http://jdem.cz/ssk92")
//    [<Test>]
//    member test.``Test jdem13`` () =
//      printfn "%s -> %A" "http://jdem.cz/sud34" (extract "http://jdem.cz/sud34")
//    [<Test>]
//    member test.``Test jdem14`` () =
//      printfn "%s -> %A" "http://jdem.cz/sse38" (extract "http://jdem.cz/sse38")
//    [<Test>]
//    member test.``Test jdem15`` () =
//      printfn "%s -> %A" "http://jdem.cz/sujd8" (extract "http://jdem.cz/sujd8")
//    [<Test>]
//    member test.``Test jdem16`` () =
//      printfn "%s -> %A" "http://jdem.cz/suku9" (extract "http://jdem.cz/suku9")
//    [<Test>]
//    member test.``Test jdem17`` () =
//      printfn "%s -> %A" "http://jdem.cz/sukv8" (extract "http://jdem.cz/sukv8")
//    [<Test>]
//    member test.``Test jdem18`` () =
//      printfn "%s -> %A" "http://jdem.cz/sukw7" (extract "http://jdem.cz/sukw7")
//    [<Test>]
//    member test.``Test jdem19`` () =
//      printfn "%s -> %A" "http://jdem.cz/suky6" (extract "http://jdem.cz/suky6")
//    [<Test>]
//    member test.``Test jdem20`` () =
//      printfn "%s -> %A" "http://jdem.cz/sukz8" (extract "http://jdem.cz/sukz8")
//    [<Test>]
//    member test.``Test jdem21`` () =
//      printfn "%s -> %A" "http://jdem.cz/sp594" (extract "http://jdem.cz/sp594")
//    [<Test>]
//    member test.``Test jdem22`` () =
//      printfn "%s -> %A" "http://jdem.cz/suru9" (extract "http://jdem.cz/suru9")
//    [<Test>]
//    member test.``Test jdem23`` () =
//      printfn "%s -> %A" "http://jdem.cz/svvf8" (extract "http://jdem.cz/svvf8")
//    [<Test>]
//    member test.``Test jdem24`` () =
//      printfn "%s -> %A" "http://jdem.cz/svt77" (extract "http://jdem.cz/svt77")
//    [<Test>]
//    member test.``Test jdem25`` () =
//      printfn "%s -> %A" "http://jdem.cz/svt77" (extract "http://jdem.cz/svt77")
//    [<Test>]
//    member test.``Test jdem26`` () =
//      printfn "%s -> %A" "http://jdem.cz/svw48" (extract "http://jdem.cz/svw48")
//    [<Test>]
//    member test.``Test jdem27`` () =
//      printfn "%s -> %A" "http://jdem.cz/svw82" (extract "http://jdem.cz/svw82")
//    [<Test>]
//    member test.``Test jdem28`` () =
//      printfn "%s -> %A" "http://jdem.cz/svxk7" (extract "http://jdem.cz/svxk7")
//    [<Test>]
//    member test.``Test jdem29`` () =
//      printfn "%s -> %A" "http://jdem.cz/svxn3" (extract "http://jdem.cz/svxn3")
//    [<Test>]
//    member test.``Test jdem30`` () =
//      printfn "%s -> %A" "http://jdem.cz/svxp3" (extract "http://jdem.cz/svxp3")
//    [<Test>]
//    member test.``Test jdem31`` () =
//      printfn "%s -> %A" "http://jdem.cz/svxt3" (extract "http://jdem.cz/svxt3")
//    [<Test>]
//    member test.``Test jdem32`` () =
//      printfn "%s -> %A" "http://jdem.cz/svyc4" (extract "http://jdem.cz/svyc4")
//    [<Test>]
//    member test.``Test jdem33`` () =
//      printfn "%s -> %A" "http://jdem.cz/sv3d8" (extract "http://jdem.cz/sv3d8")
//    [<Test>]
//    member test.``Test jdem34`` () =
//      printfn "%s -> %A" "http://jdem.cz/sv3e3" (extract "http://jdem.cz/sv3e3")
//    [<Test>]
//    member test.``Test jdem35`` () =
//      printfn "%s -> %A" "http://jdem.cz/sv3f3" (extract "http://jdem.cz/sv3f3")
//    [<Test>]
//    member test.``Test jdem36`` () =
//      printfn "%s -> %A" "http://jdem.cz/sv3g6" (extract "http://jdem.cz/sv3g6")
//    [<Test>]
//    member test.``Test jdem37`` () =
//      printfn "%s -> %A" "http://jdem.cz/sv4m8" (extract "http://jdem.cz/sv4m8")
//    [<Test>]
//    member test.``Test jdem38`` () =
//      printfn "%s -> %A" "http://jdem.cz/sv5r7" (extract "http://jdem.cz/sv5r7")
//    [<Test>]
//    member test.``Test jdem39`` () =
//      printfn "%s -> %A" "http://jdem.cz/sv5t3" (extract "http://jdem.cz/sv5t3")
//    [<Test>]
//    member test.``Test jdem40`` () =
//      printfn "%s -> %A" "http://jdem.cz/swbh4" (extract "http://jdem.cz/swbh4")
//    [<Test>]
//    member test.``Test jdem41`` () =
//      printfn "%s -> %A" "http://jdem.cz/swbr8" (extract "http://jdem.cz/swbr8")
//    [<Test>]
//    member test.``Test jdem42`` () =
//      printfn "%s -> %A" "http://jdem.cz/swbs9" (extract "http://jdem.cz/swbs9")
//    [<Test>]
//    member test.``Test jdem43`` () =
//      printfn "%s -> %A" "http://jdem.cz/swbt7" (extract "http://jdem.cz/swbt7")
//    [<Test>]
//    member test.``Test jdem44`` () =
//      printfn "%s -> %A" "http://jdem.cz/swbv9" (extract "http://jdem.cz/swbv9")
//    [<Test>]
//    member test.``Test jdem45`` () =
//      printfn "%s -> %A" "http://jdem.cz/sw4c6" (extract "http://jdem.cz/sw4c6")
//    [<Test>]
//    member test.``Test jdem46`` () =
//      printfn "%s -> %A" "http://jdem.cz/sw4d4" (extract "http://jdem.cz/sw4d4")
//    [<Test>]
//    member test.``Test jdem47`` () =
//      printfn "%s -> %A" "http://jdem.cz/sw4e3" (extract "http://jdem.cz/sw4e3")
//    [<Test>]
//    member test.``Test jdem48`` () =
//      printfn "%s -> %A" "http://jdem.cz/sw4f7" (extract "http://jdem.cz/sw4f7")
//    [<Test>]
//    member test.``Test jdem49`` () =
//      printfn "%s -> %A" "http://jdem.cz/sw4g5" (extract "http://jdem.cz/sw4g5")
//    [<Test>]
//    member test.``Test jdem50`` () =
//      printfn "%s -> %A" "http://jdem.cz/sw835" (extract "http://jdem.cz/sw835")
//    [<Test>]
//    member test.``Test jdem51`` () =
//      printfn "%s -> %A" "http://jdem.cz/sw857" (extract "http://jdem.cz/sw857")
//    [<Test>]
//    member test.``Test jdem52`` () =
//      printfn "%s -> %A" "http://jdem.cz/sw864" (extract "http://jdem.cz/sw864")
//    [<Test>]
//    member test.``Test jdem53`` () =
//      printfn "%s -> %A" "http://jdem.cz/sw875" (extract "http://jdem.cz/sw875")
//    [<Test>]
//    member test.``Test jdem54`` () =
//      printfn "%s -> %A" "http://jdem.cz/sw883" (extract "http://jdem.cz/sw883")
//    [<Test>]
//    member test.``Test jdem55`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxcq4" (extract "http://jdem.cz/sxcq4")
//    [<Test>]
//    member test.``Test jdem56`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxjd6" (extract "http://jdem.cz/sxjd6")
//    [<Test>]
//    member test.``Test jdem57`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxkn5" (extract "http://jdem.cz/sxkn5")
//    [<Test>]
//    member test.``Test jdem58`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxkq2" (extract "http://jdem.cz/sxkq2")
//    [<Test>]
//    member test.``Test jdem59`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxkr3" (extract "http://jdem.cz/sxkr3")
//    [<Test>]
//    member test.``Test jdem60`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxjj3" (extract "http://jdem.cz/sxjj3")
//    [<Test>]
//    member test.``Test jdem61`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxkx3" (extract "http://jdem.cz/sxkx3")
//    [<Test>]
//    member test.``Test jdem62`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxrz9" (extract "http://jdem.cz/sxrz9")
//    [<Test>]
//    member test.``Test jdem63`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxr49" (extract "http://jdem.cz/sxr49")
//    [<Test>]
//    member test.``Test jdem64`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxr57" (extract "http://jdem.cz/sxr57")
//    [<Test>]
//    member test.``Test jdem65`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxr78" (extract "http://jdem.cz/sxr78")
//    [<Test>]
//    member test.``Test jdem66`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxr83" (extract "http://jdem.cz/sxr83")
//    [<Test>]
//    member test.``Test jdem67`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxr99" (extract "http://jdem.cz/sxr99")
//    [<Test>]
//    member test.``Test jdem68`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxsa7" (extract "http://jdem.cz/sxsa7")
//    [<Test>]
//    member test.``Test jdem69`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxse9" (extract "http://jdem.cz/sxse9")
//    [<Test>]
//    member test.``Test jdem70`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxtc6" (extract "http://jdem.cz/sxtc6")
//    [<Test>]
//    member test.``Test jdem71`` () =
//      printfn "%s -> %A" "http://jdem.cz/sxtk9" (extract "http://jdem.cz/sxtk9")
//    [<Test>]
//    member test.``Test jdem72`` () =
//      printfn "%s -> %A" "http://jdem.cz/sybr5" (extract "http://jdem.cz/sybr5")
//    [<Test>]
//    member test.``Test jdem73`` () =
//      printfn "%s -> %A" "http://jdem.cz/sybt9" (extract "http://jdem.cz/sybt9")
//    [<Test>]
//    member test.``Test jdem74`` () =
//      printfn "%s -> %A" "http://jdem.cz/sych8" (extract "http://jdem.cz/sych8")
//    [<Test>]
//    member test.``Test jdem75`` () =
//      printfn "%s -> %A" "http://jdem.cz/sycy8" (extract "http://jdem.cz/sycy8")
//    [<Test>]
//    member test.``Test jdem76`` () =
//      printfn "%s -> %A" "http://jdem.cz/syda3" (extract "http://jdem.cz/syda3")
//    [<Test>]
//    member test.``Test jdem77`` () =
//      printfn "%s -> %A" "http://jdem.cz/sydc8" (extract "http://jdem.cz/sydc8")
//    [<Test>]
//    member test.``Test jdem78`` () =
//      printfn "%s -> %A" "http://jdem.cz/syde6" (extract "http://jdem.cz/syde6")
//    [<Test>]
//    member test.``Test jdem79`` () =
//      printfn "%s -> %A" "http://jdem.cz/sydp9" (extract "http://jdem.cz/sydp9")
//    [<Test>]
//    member test.``Test jdem80`` () =
//      printfn "%s -> %A" "http://jdem.cz/symr9" (extract "http://jdem.cz/symr9")
//    [<Test>]
//    member test.``Test jdem81`` () =
//      printfn "%s -> %A" "http://jdem.cz/syms8" (extract "http://jdem.cz/syms8")
//    [<Test>]
//    member test.``Test jdem82`` () =
//      printfn "%s -> %A" "http://jdem.cz/symt5" (extract "http://jdem.cz/symt5")
//    [<Test>]
//    member test.``Test jdem83`` () =
//      printfn "%s -> %A" "http://jdem.cz/symu9" (extract "http://jdem.cz/symu9")
//    [<Test>]
//    member test.``Test jdem84`` () =
//      printfn "%s -> %A" "http://jdem.cz/symw7" (extract "http://jdem.cz/symw7")
//    [<Test>]
//    member test.``Test jdem85`` () =
//      printfn "%s -> %A" "http://jdem.cz/symx5" (extract "http://jdem.cz/symx5")
//    [<Test>]
//    member test.``Test jdem86`` () =
//      printfn "%s -> %A" "http://jdem.cz/sw8k6" (extract "http://jdem.cz/sw8k6")
//    [<Test>]
//    member test.``Test jdem87`` () =
//      printfn "%s -> %A" "http://jdem.cz/sysz8" (extract "http://jdem.cz/sysz8")
//    [<Test>]
//    member test.``Test jdem88`` () =
//      printfn "%s -> %A" "http://jdem.cz/sys35" (extract "http://jdem.cz/sys35")
//    [<Test>]
//    member test.``Test jdem89`` () =
//      printfn "%s -> %A" "http://jdem.cz/sys45" (extract "http://jdem.cz/sys45")
//    [<Test>]
//    member test.``Test jdem90`` () =
//      printfn "%s -> %A" "http://jdem.cz/sys57" (extract "http://jdem.cz/sys57")
//    [<Test>]
//    member test.``Test jdem91`` () =
//      printfn "%s -> %A" "http://jdem.cz/sytt2" (extract "http://jdem.cz/sytt2")
//    [<Test>]
//    member test.``Test jdem92`` () =
//      printfn "%s -> %A" "http://jdem.cz/szqa6" (extract "http://jdem.cz/szqa6")
//    [<Test>]
//    member test.``Test jdem93`` () =
//      printfn "%s -> %A" "http://jdem.cz/szq32" (extract "http://jdem.cz/szq32")
//    [<Test>]
//    member test.``Test jdem94`` () =
//      printfn "%s -> %A" "http://jdem.cz/szq92" (extract "http://jdem.cz/szq92")
//    [<Test>]
//    member test.``Test jdem95`` () =
//      printfn "%s -> %A" "http://jdem.cz/szra6" (extract "http://jdem.cz/szra6")
//    [<Test>]
//    member test.``Test jdem96`` () =
//      printfn "%s -> %A" "http://jdem.cz/szrb8" (extract "http://jdem.cz/szrb8")
//    [<Test>]
//    member test.``Test jdem97`` () =
//      printfn "%s -> %A" "http://jdem.cz/szrc7" (extract "http://jdem.cz/szrc7")
//    [<Test>]
//    member test.``Test jdem98`` () =
//      printfn "%s -> %A" "http://jdem.cz/szrd3" (extract "http://jdem.cz/szrd3")
//    [<Test>]
//    member test.``Test jdem99`` () =
//      printfn "%s -> %A" "http://jdem.cz/szrs6" (extract "http://jdem.cz/szrs6")
//    [<Test>]
//    member test.``Test jdem100`` () =
//      printfn "%s -> %A" "http://jdem.cz/szv72" (extract "http://jdem.cz/szv72")
//    [<Test>]
//    member test.``Test jdem101`` () =
//      printfn "%s -> %A" "http://jdem.cz/szv87" (extract "http://jdem.cz/szv87")
//    [<Test>]
//    member test.``Test jdem102`` () =
//      printfn "%s -> %A" "http://jdem.cz/szwb3" (extract "http://jdem.cz/szwb3")
//    [<Test>]
//    member test.``Test jdem103`` () =
//      printfn "%s -> %A" "http://jdem.cz/szwc8" (extract "http://jdem.cz/szwc8")
//    [<Test>]
//    member test.``Test jdem104`` () =
//      printfn "%s -> %A" "http://jdem.cz/szxp9" (extract "http://jdem.cz/szxp9")
//    [<Test>]
//    member test.``Test jdem105`` () =
//      printfn "%s -> %A" "http://jdem.cz/szyn4" (extract "http://jdem.cz/szyn4")
//    [<Test>]
//    member test.``Test jdem106`` () =
//      printfn "%s -> %A" "http://jdem.cz/szz33" (extract "http://jdem.cz/szz33")
//    [<Test>]
//    member test.``Test jdem107`` () =
//      printfn "%s -> %A" "http://jdem.cz/sz2y4" (extract "http://jdem.cz/sz2y4")
//    [<Test>]
//    member test.``Test jdem108`` () =
//      printfn "%s -> %A" "http://jdem.cz/sz2z8" (extract "http://jdem.cz/sz2z8")
//    [<Test>]
//    member test.``Test jdem109`` () =
//      printfn "%s -> %A" "http://jdem.cz/sz228" (extract "http://jdem.cz/sz228")
//    [<Test>]
//    member test.``Test jdem110`` () =
//      printfn "%s -> %A" "http://jdem.cz/sz232" (extract "http://jdem.cz/sz232")
//    [<Test>]
//    member test.``Test jdem111`` () =
//      printfn "%s -> %A" "http://jdem.cz/sz249" (extract "http://jdem.cz/sz249")
//    [<Test>]
//    member test.``Test jdem112`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2b92" (extract "http://jdem.cz/s2b92")
//    [<Test>]
//    member test.``Test jdem113`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2ca3" (extract "http://jdem.cz/s2ca3")
//    [<Test>]
//    member test.``Test jdem114`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2cb8" (extract "http://jdem.cz/s2cb8")
//    [<Test>]
//    member test.``Test jdem115`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2cc7" (extract "http://jdem.cz/s2cc7")
//    [<Test>]
//    member test.``Test jdem116`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2cd9" (extract "http://jdem.cz/s2cd9")
//    [<Test>]
//    member test.``Test jdem117`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2fh9" (extract "http://jdem.cz/s2fh9")
//    [<Test>]
//    member test.``Test jdem118`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2pm7" (extract "http://jdem.cz/s2pm7")
//    [<Test>]
//    member test.``Test jdem119`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2pn6" (extract "http://jdem.cz/s2pn6")
//    [<Test>]
//    member test.``Test jdem120`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2pp5" (extract "http://jdem.cz/s2pp5")
//    [<Test>]
//    member test.``Test jdem121`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2pr5" (extract "http://jdem.cz/s2pr5")
//    [<Test>]
//    member test.``Test jdem122`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2pq3" (extract "http://jdem.cz/s2pq3")
//    [<Test>]
//    member test.``Test jdem123`` () =
//      printfn "%s -> %A" "http://jdem.cz/sz759" (extract "http://jdem.cz/sz759")
//    [<Test>]
//    member test.``Test jdem124`` () =
//      printfn "%s -> %A" "http://jdem.cz/sz765" (extract "http://jdem.cz/sz765")
//    [<Test>]
//    member test.``Test jdem125`` () =
//      printfn "%s -> %A" "http://jdem.cz/sz776" (extract "http://jdem.cz/sz776")
//    [<Test>]
//    member test.``Test jdem126`` () =
//      printfn "%s -> %A" "http://jdem.cz/sz782" (extract "http://jdem.cz/sz782")
//    [<Test>]
//    member test.``Test jdem127`` () =
//      printfn "%s -> %A" "http://jdem.cz/sz795" (extract "http://jdem.cz/sz795")
//    [<Test>]
//    member test.``Test jdem128`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2u59" (extract "http://jdem.cz/s2u59")
//    [<Test>]
//    member test.``Test jdem129`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2u63" (extract "http://jdem.cz/s2u63")
//    [<Test>]
//    member test.``Test jdem130`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2u89" (extract "http://jdem.cz/s2u89")
//    [<Test>]
//    member test.``Test jdem131`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2u97" (extract "http://jdem.cz/s2u97")
//    [<Test>]
//    member test.``Test jdem132`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2va7" (extract "http://jdem.cz/s2va7")
//    [<Test>]
//    member test.``Test jdem133`` () =
//      printfn "%s -> %A" "http://jdem.cz/s2vb5" (extract "http://jdem.cz/s2vb5")