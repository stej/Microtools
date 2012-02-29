module test.textSplitter

open NUnit.Framework
open FsUnit
open TextSplitter

type ``Given some texts to split`` ()=

    [<Test>] 
    member test.``Text with no url should return only FragmentWords`` () =
        let f = TextSplitter.splitText "this is only test"
        f.Length |> should equal 1
        match f.[0] with
        | FragmentWords(w) -> w |> should equal "this is only test"
        | _                -> failwith "there is no url"

    [<Test>] 
    member test.``Text with hash only should return only FragmentHash`` () =
        let f = TextSplitter.splitText "#abc"
        f.Length |> should equal 1
        match f.[0] with
        | FragmentHash(w) -> w |> should equal "#abc"
        | _               -> failwith "hash not parsed"

    [<Test>] 
    member test.``Text with url only should return only FragmentUrl`` () =
        let f = TextSplitter.splitText "http://www.google.com"
        f.Length |> should equal 1
        match f.[0] with
        | FragmentUrl(w) -> w |> should equal "http://www.google.com"
        | _              -> failwith "url not parsed"

    [<Test>] 
    member test.``Text with mention only should return only FragmentUserMention`` () =
        let f = TextSplitter.splitText "@user1"
        f.Length |> should equal 1
        match f.[0] with
        | FragmentUserMention(w) -> w |> should equal "@user1"
        | _                      -> failwith "mention not parsed"

    [<Test>] 
    member test.``Text with t.co link is correctly parsed`` () =
        let f = TextSplitter.splitText "test http://t.co/abc"
        f.Length |> should equal 2
        match f.[1] with
        | FragmentUrl(w) -> w |> should equal "http://t.co/abc"
        | _              -> failwith "mention not parsed"

    [<Test>] 
    member test.``Text with all types returns correctly parsed fragments`` () =
        let f = TextSplitter.splitText "user @user1 reported abuse at http://google.com #bug #bugg cc @user2"
        f.Length |> should equal 10
        match f with
        | [|FragmentWords("user ")
            FragmentUserMention("@user1")
            FragmentWords(" reported abuse at ")
            FragmentUrl("http://google.com")
            FragmentWords(" ")
            FragmentHash("#bug")
            FragmentWords(" ")
            FragmentHash("#bugg")
            FragmentWords(" cc ")
            FragmentUserMention("@user2")|] -> ()
        | _ -> failwith ("no match" + f.ToString())