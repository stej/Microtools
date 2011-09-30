module test.test

open NUnit.Framework
open FsUnit

[<TestFixture>] 
type ``Test working testing environment`` ()=
    let myTrue = true

    [<Test>] member test.
      env () =
            myTrue |> should be True