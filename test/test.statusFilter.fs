module test.statusFilter

open FsUnit
open NUnit.Framework
open System
open StatusFilter

type ``Test status filtering`` ()=

    let blackOne item   = Some({ FilterType = BlackList; Items = [item]})
    let blackMore items = Some({ FilterType = BlackList; Items = items})

    [<Test>]
    member test.``parse regex`` () =
        parseFilter "#r:'abc'"       |> should equal <| blackOne (Regex("abc"))
        parseFilter "#r:    'abc'"   |> should equal <| blackOne (Regex("abc"))
        parseFilter "#r:'(a'"        |> should equal None
        parseFilter @"#r:'\n\t\'x'"  |> should equal <| blackOne (Regex("\\n\\t'x"))
        parseFilter @"#r:'+'"        |> should equal None
    
    [<Test>]
    member test.``parse text in apostrhophes`` () =
        parseFilter @"'\n\t\'x'"  |> should equal <| blackOne (StatusText("\\n\\t'x"))
        parseFilter "'a b c'"     |> should equal <| blackOne (StatusText("a b c"))
    
    [<Test>]
    member test.``parse ordinal text`` () =
        parseFilter "abc"         |> should equal <| blackOne (StatusText("abc"))
        parseFilter " abe"        |> should equal <| blackOne (StatusText("abe"))

    [<Test>]
    member test.``parse ordinal text with stars`` () =
        parseFilter "*abc"        |> should equal <| blackOne (StatusText("*abc"))
        parseFilter "abc*"        |> should equal <| blackOne (StatusText("abc*"))
        parseFilter "*abc*"       |> should equal <| blackOne (StatusText("*abc*"))

    [<Test>]
    member test.``parse user reference`` () =
        parseFilter "@userx"        |> should equal <| blackOne (User("userx"))
        parseFilter "@"             |> should equal None

    [<Test>]
    member test.``parse all retweets`` () =
        parseFilter "rt@all"        |> should equal <| blackOne (AllRetweets)

    [<Test>]
    member test.``parse retweet by user`` () =
        parseFilter "rt@userx"      |> should equal <| blackOne (UserRetweet("userx"))
        parseFilter "rt@"           |> should equal None

    [<Test>]
    member test.``parse timeline by user`` () =
        parseFilter "timeline@usax" |> should equal <| blackOne (UserTimeline("usax"))

    [<Test>]
    member test.``parse all timeline`` () =
        parseFilter "timeline@all"  |> should equal <| blackOne (AllTimeline)

    [<Test>]
    member test.``parse filter reference`` () =
        parseFilter "#f:filter-test"   |> should equal <| blackOne (User("user-filter-test"))
        parseFilter "#f:filter-x_y_"   |> should equal <| blackOne (User("user-filter-x_y_"))
        parseFilter "#f:"              |> should equal None
        parseFilter "#f"               |> should equal None

    [<Test>]
    member test.``parse invalid input with hash`` () =
        parseFilter "#"                |> should equal None

    [<Test>]
    member test.``parse complex filter`` () =
        parseFilter "@userx a-_$%( abc 'text in apo' #r:'neco' #r:'\\d+necox \\t \\' abc\\d+' timeline@ab timeline@all rt@all rt@userxyz #f:filter-test #f:filter-x_2_0-end"  
        |> should equal <| blackMore [User("userx")
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
                                      User("user-filter-x_2_0-end")]

type ``Test status filtering - white list`` () =
    let whiteOne item   = Some({ FilterType = WhiteList; Items = [item]})
    let whiteMore items = Some({ FilterType = WhiteList; Items = items})

    [<Test>]
    member test.``parse regex`` () =
        parseFilter "#wl #r:'abc'"       |> should equal <| whiteOne (Regex("abc"))
        parseFilter "#wl #r:    'abc'"   |> should equal <| whiteOne (Regex("abc"))
        parseFilter "#wl #r:'(a'"        |> should equal None
        parseFilter @"#wl #r:'\n\t\'x'"  |> should equal <| whiteOne (Regex("\\n\\t'x"))
        parseFilter @"#wl #r:'+'"        |> should equal None
    
    [<Test>]
    member test.``parse text in apostrhophes`` () =
        parseFilter @"#wl '\n\t\'x'"  |> should equal <| whiteOne (StatusText("\\n\\t'x"))
        parseFilter "#wl 'a b c'"     |> should equal <| whiteOne (StatusText("a b c"))
    
    [<Test>]
    member test.``parse ordinal text`` () =
        parseFilter "#wl abc"         |> should equal <| whiteOne (StatusText("abc"))
        parseFilter "#wl abe"        |> should equal <| whiteOne (StatusText("abe"))

    [<Test>]
    member test.``parse ordinal text with stars`` () =
        parseFilter "#wl *abc"        |> should equal <| whiteOne (StatusText("*abc"))
        parseFilter "#wl abc*"        |> should equal <| whiteOne (StatusText("abc*"))
        parseFilter "#wl *abc*"       |> should equal <| whiteOne (StatusText("*abc*"))

    [<Test>]
    member test.``parse user reference`` () =
        parseFilter "#wl @userx"        |> should equal <| whiteOne (User("userx"))
        parseFilter "#wl @"             |> should equal None

    [<Test>]
    member test.``parse all retweets`` () =
        parseFilter "#wl rt@all"        |> should equal <| whiteOne (AllRetweets)

    [<Test>]
    member test.``parse retweet by user`` () =
        parseFilter "#wl rt@userx"      |> should equal <| whiteOne (UserRetweet("userx"))
        parseFilter "#wl rt@"           |> should equal None

    [<Test>]
    member test.``parse timeline by user`` () =
        parseFilter "#wl timeline@usax" |> should equal <| whiteOne (UserTimeline("usax"))

    [<Test>]
    member test.``parse all timeline`` () =
        parseFilter "#wl timeline@all"  |> should equal <| whiteOne (AllTimeline)

    [<Test>]
    member test.``parse filter reference`` () =
        parseFilter "#wl #f:filter-test"   |> should equal <| whiteOne (User("user-filter-test"))
        parseFilter "#wl #f:filter-x_y_"   |> should equal <| whiteOne (User("user-filter-x_y_"))
        parseFilter "#wl #f:"              |> should equal None

    [<Test>]
    member test.``#whitelist alias works``() =
        parseFilter "#whitelist #f:filter-test"   |> should equal <| whiteOne (User("user-filter-test"))
        parseFilter "#whitelist timeline@all"  |> should equal <| whiteOne (AllTimeline)
        parseFilter "#whitelist #r:'abc'"       |> should equal <| whiteOne (Regex("abc"))
        parseFilter @"#whitelist '\n\t\'x'"  |> should equal <| whiteOne (StatusText("\\n\\t'x"))
        parseFilter "#whitelist 'a b c'"     |> should equal <| whiteOne (StatusText("a b c"))
        parseFilter "#whitelist abc"         |> should equal <| whiteOne (StatusText("abc"))
        parseFilter "#whitelist *abc"        |> should equal <| whiteOne (StatusText("*abc"))
        parseFilter "#whitelist @userx"        |> should equal <| whiteOne (User("userx"))
        parseFilter "#whitelist @"             |> should equal None
        parseFilter "#whitelist rt@all"        |> should equal <| whiteOne (AllRetweets)
        parseFilter "#whitelist rt@"           |> should equal None
        parseFilter "#whitelist timeline@usax" |> should equal <| whiteOne (UserTimeline("usax"))

    [<Test>]
    member test.``parse complex filter`` () =
        parseFilter "#wl @userx a-_$%( abc 'text in apo' #r:'neco' #r:'\\d+necox \\t \\' abc\\d+' timeline@ab timeline@all rt@all rt@userxyz #f:filter-test #f:filter-x_2_0-end"  
        |> should equal <| whiteMore [User("userx")
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
                                      User("user-filter-x_2_0-end")]