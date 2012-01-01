module test.testShortenedUrlToDomain

open NUnit.Framework
open FsUnit
open Utils

[<Test>] 
let ``Shorten http urls with 2 parts a`` () =
    shortenUrlToDomain "http://t.co"         |> should equal "http://t.co"

[<Test>] 
let ``Shorten http urls with 2 parts b`` () =
    shortenUrlToDomain "http://t.co/"        |> should equal "http://t.co"

[<Test>] 
let ``Shorten http urls with 2 parts c`` () =
    shortenUrlToDomain "http://t.co/abc"         |> should equal "http://t.co/..."

[<Test>] 
let ``Shorten http urls with 2 parts d`` () =
    shortenUrlToDomain "http://t.co/abc/def"     |> should equal "http://t.co/..."

[<Test>] 
let ``Shorten http urls with 2 parts e`` () =
    shortenUrlToDomain "http://t.co/abc.xyz/def" |> should equal "http://t.co/..."

[<Test>]
let ``Shorten http urls with 3 parts a`` () =
    shortenUrlToDomain "http://www.seznam.cz"           |> should equal "http://www.seznam.cz"

[<Test>]
let ``Shorten http urls with 3 parts b`` () =
    shortenUrlToDomain "http://www.seznam.cz/"          |> should equal "http://www.seznam.cz"

[<Test>]
let ``Shorten http urls with 3 parts c`` () =
    shortenUrlToDomain "http://www.seznam.cz/abc"       |> should equal "http://www.seznam.cz/..."

[<Test>]
let ``Shorten http urls with 3 parts d`` () =
    shortenUrlToDomain "http://www.seznam.cz/abc.cz"    |> should equal "http://www.seznam.cz/..."

[<Test>]
let ``Shorten http urls with 3 parts e`` () =
    shortenUrlToDomain "http://www.seznam.cz/abc.cz/"   |> should equal "http://www.seznam.cz/..."

[<Test>]
let ``Shorten http urls with 3 parts f`` () =
    shortenUrlToDomain "http://www.seznam.cz/abc.cz/ax" |> should equal "http://www.seznam.cz/..."

[<Test>]
let ``Shorten https urls a`` () =
    shortenUrlToDomain "https://www.seznam.cz"      |> should equal "https://www.seznam.cz"

[<Test>]
let ``Shorten https urls b`` () =
    shortenUrlToDomain "https://www.seznam.cz/"     |> should equal "https://www.seznam.cz"

[<Test>]
let ``Shorten https urls c`` () =
    shortenUrlToDomain "https://www.seznam.cz/abc"  |> should equal "https://www.seznam.cz/..."

[<Test>]
let ``Shorten https urls d`` () =
    shortenUrlToDomain "https://t.co"     |> should equal "https://t.co"

[<Test>]
let ``Shorten https urls e`` () =
    shortenUrlToDomain "https://t.co/"    |> should equal "https://t.co"

[<Test>]
let ``Shorten https urls f`` () =
    shortenUrlToDomain "https://t.co/abc" |> should equal "https://t.co/..."

[<Test>]
let ``Bug 0`` () =
    shortenUrlToDomain "http://powershellgroup.org/charlotte.nc" |> should equal "http://powershellgroup.org/..."