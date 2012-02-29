module test.bug2

open NUnit.Framework
open FsUnit
open TextSplitter

[<TestFixture>] 
type ``Client fails when link in status ends with parenthesis - issue 2`` () =

    [<Test>]
    member test.``Test for status ending with url and parenthesis`` () =
        let f = "@alesroubicek @borekb Pokud se něco nezměnilo, tak mobi formát jsem našel až po přihlášení k betě uživ. účtu (https://account.manning.com)."
                |> TextSplitter.splitText

        f.Length |> should equal 6
        match f.[4] with
        | FragmentUrl(w) -> w |> should equal "https://account.manning.com"
        | _              -> failwith "bad url"

    [<Test>]
    member test.``Test for status ending with url only`` () =
        let f = "@alesroubicek @borekb Pokud se něco nezměnilo, tak mobi formát jsem našel až po přihlášení k betě uživ. účtu (https://account.manning.com"
                |> TextSplitter.splitText

        f.Length |> should equal 5
        match f.[4] with
        | FragmentUrl(w) -> w |> should equal "https://account.manning.com"
        | _              -> failwith "bad url"

