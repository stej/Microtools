module test.statusFilter

open FsUnit
open NUnit.Framework
open System
open StatusFilter

type ``Test status filtering`` ()=

    [<Test>]
    member test.``parse regex`` () =
        parseFilter "#r:'abc'"       |> should equal (Some([Regex("abc")]))
        parseFilter "#r:    'abc'"   |> should equal (Some([Regex("abc")]))
        parseFilter "#r:'(a'"        |> should equal None
        parseFilter @"#r:'\n\t\'x'"  |> should equal (Some([Regex("\\n\\t'x")]))
        parseFilter @"#r:'+'"        |> should equal None
    
    [<Test>]
    member test.``parse text in apostrhophes`` () =
        parseFilter @"'\n\t\'x'"  |> should equal (Some([StatusText("\\n\\t'x")]))
        parseFilter "'a b c'"     |> should equal (Some([StatusText("a b c")]))
    
    [<Test>]
    member test.``parse ordinal text`` () =
        parseFilter "abc"         |> should equal (Some([StatusText("abc")]))
        parseFilter " abe"        |> should equal (Some([StatusText("abe")]))

    [<Test>]
    member test.``parse ordinal text with stars`` () =
        parseFilter "*abc"        |> should equal (Some([StatusText("*abc")]))
        parseFilter "abc*"        |> should equal (Some([StatusText("abc*")]))
        parseFilter "*abc*"       |> should equal (Some([StatusText("*abc*")]))

    [<Test>]
    member test.``parse user reference`` () =
        parseFilter "@userx"        |> should equal (Some([User("userx")]))
        parseFilter "@"             |> should equal None

    [<Test>]
    member test.``parse all retweets`` () =
        parseFilter "rt@all"        |> should equal (Some([AllRetweets]))

    [<Test>]
    member test.``parse retweet by user`` () =
        parseFilter "rt@userx"      |> should equal (Some([UserRetweet("userx")]))
        parseFilter "rt@"           |> should equal None

    [<Test>]
    member test.``parse timeline by user`` () =
        parseFilter "timeline@usax" |> should equal (Some([UserTimeline("usax")]))

    [<Test>]
    member test.``parse all timeline`` () =
        parseFilter "timeline@all"  |> should equal (Some([AllTimeline]))

    [<Test>]
    member test.``parse filter reference`` () =
        parseFilter "#f:filter-test"   |> should equal (Some([User("user-filter-test")]))
        parseFilter "#f:filter-x_y_"   |> should equal (Some([User("user-filter-x_y_")]))
        parseFilter "#f:"              |> should equal None
        parseFilter "#f"               |> should equal None

    [<Test>]
    member test.``parse invalid input with hash`` () =
        parseFilter "#"                |> should equal None

    [<Test>]
    member test.``parse complex filter`` () =
        parseFilter "@userx a-_$%( abc 'text in apo' #r:'neco' #r:'\\d+necox \\t \\' abc\\d+' timeline@ab timeline@all rt@all rt@userxyz #f:filter-test #f:filter-x_2_0-end"  
        |> should equal (Some([User("userx")
                               StatusText("a-_$%(")
                               StatusText("abc")
                               StatusText("text in apo")
                               Regex("neco")
                               Regex("\\d+necox \\t ' abc\\d+")
                               UserTimeline("ab")
                               AllTimeline
                               AllRetweets
                               UserRetweet("userxyz")
                               User("user-filter-test")
                               User("user-filter-x_2_0-end")
                              ]))