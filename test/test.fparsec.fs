module test.fparsec

open FParsec
open FsUnit
open NUnit.Framework

type StringConstant = StringConstant of string * string

type FilterItem = 
    | Regex of string
    | StringNoWhitespace of string
    | StringWithWhitespace of string
    | User of string
    | UserRetweet of string
    | AllTimeline
    | AllRetweets
    | FilterReference of string

type ``Test fparsec`` ()=
    let testp p str =
        match run p str with
        | Success(result, _, _)   -> printfn "Success: %A" result; Some(result)
        | Failure(errorMsg, _, _) -> printfn "Failure: %s" errorMsg; None
    let ws = spaces
    let str_ws s = pstring s .>> ws
    let float_ws = pfloat .>> ws

    [<Test>]
    member test.``twClient.Filter`` () =
        let filterParser =
            let stringInApostrophes = 
                // todo: zrejme by slo i pomoci noneOf (pripadne nejakych satisfy)
                let escaped = pipe2 (pstring "\\") 
                                    (anyString 1) 
                                    (fun a b -> if b="'"then b else a+b)

                let normalCharSnippet = manySatisfy (fun c -> c <> ''' && c <> '\\')
                let normalOrEscapedCharSnippet  = 
                    stringsSepBy normalCharSnippet escaped
                between (pstring "'") (pstring "'") normalOrEscapedCharSnippet 
            let regex = 
                pstring "#r:" 
                    >>. spaces
                    >>. stringInApostrophes
                    |>> (fun s -> s.Trim(''') |> Regex)
            let simpleText  = many1Satisfy isLetter |>> StringNoWhitespace
            let user        = pstring "@" >>. many1Satisfy isLetter   |>> User
            let allTimeline = pstring "@all"                         |>> ignore |>> (fun _ -> AllTimeline)
            let userRetweet = pstring "rt@" >>. many1Satisfy isLetter |>> UserRetweet
            let allRetweets = pstring "rt@all"                       |>> ignore |>> (fun _ -> AllRetweets)
            let filterRef   = pstring "#f:" >>. many1Satisfy (fun c -> isLetter c || isDigit c || c = '-' || c = '_') |>> FilterReference
            let parsers = 
                choice [allRetweets
                        userRetweet
                        allTimeline
                        regex
                        stringInApostrophes |>> StringWithWhitespace
                        simpleText
                        user
                        filterRef]
            //spaces >>. (stringsSepBy parsers (skipAnyOf ' ')) .>> spaces .>> eof
            spaces >>. (sepBy parsers (skipAnyOf " ")) .>> spaces .>> eof

        testp filterParser "#r:'abc'"       |> should equal (Some([Regex("abc")]))
        testp filterParser "#r:    'abc'"   |> should equal (Some([Regex("abc")]))
        testp filterParser "#r:'(a'"        |> should equal (Some([Regex("(a")]))
        testp filterParser @"#r:'\n\t\'x'"  |> should equal (Some([Regex("\\n\\t'x")]))
    
        testp filterParser @"'\n\t\'x'"  |> should equal (Some([StringWithWhitespace("\\n\\t'x")]))
        testp filterParser "'a b c'"     |> should equal (Some([StringWithWhitespace("a b c")]))
        testp filterParser "abc"         |> should equal (Some([StringNoWhitespace("abc")]))

        testp filterParser "@all"    |> should equal (Some([AllTimeline]))
        testp filterParser "@userx"  |> should equal (Some([User("userx")]))
        testp filterParser "@"       |> should equal None
        testp filterParser "rt@all"  |> should equal (Some([AllRetweets]))
        testp filterParser "rt@userx"|> should equal (Some([UserRetweet("userx")]))
        testp filterParser "rt@"     |> should equal None

        testp filterParser "#f:filter-ref_x"  |> should equal (Some([FilterReference("filter-ref_x")]))
        testp filterParser "#f:"              |> should equal None
        testp filterParser "#f"               |> should equal None

        testp filterParser "#"                |> should equal None

        testp filterParser "@userx abc #r:'neco' #r:'\\d+necox \\t \\' abc\\d+' @all rt@all rt@userxyz #f:f1 #f:f2_v2-0"  
        |> should equal (Some([User("userx")
                               StringNoWhitespace("abc")
                               Regex("neco")
                               Regex("\\d+necox \\t ' abc\\d+")
                               AllTimeline
                               AllRetweets
                               UserRetweet("userxyz")
                               FilterReference("f1")
                               FilterReference("f2_v2-0")
                              ]))

    [<Test>] 
    member test.``tfloat`` () =
        testp pfloat "1.2" |> should equal (Some(1.2))

    [<Test>] 
    member test.``brackets`` () =
        let floatBetweenBrackets = pstring "[" >>. pfloat .>> pstring "]"
        testp floatBetweenBrackets "[1.2]" |> should equal (Some(1.2))
        testp floatBetweenBrackets "[]"  |> should equal None
        testp floatBetweenBrackets "[1." |> should equal None
        testp floatBetweenBrackets "1.]" |> should equal None

    [<Test>]
    member test.``between strings`` () =
        //let betweenStrings s1 s2 p = pstring s1 >>. p .>> pstring s2
        let floatBetweenDoubleBrackets = pfloat |> between (pstring "[[") (pstring "]]")
        testp floatBetweenDoubleBrackets "[[1.2]]" |> should equal (Some(1.2))

    [<Test>]
    member test.``many between strings`` () =
        let floatBetweenDoubleBrackets = pfloat |> between (pstring "[[") (pstring "]]")
        let manyf = many floatBetweenDoubleBrackets
        testp manyf "[[1.2]]" |> should equal (Some([1.2]))
        testp manyf "[[1.2]][[3]][[4]]" |> should equal (Some([1.2; 3.; 4.]))

    [<Test>]
    member test.``skip`` () =
        testp (skipMany (pstring "test") >>. pfloat) "testtest1.0" |> should equal (Some(1.0))
        testp (skipMany (pstring "test") >>. pfloat) "testtest" |> should equal None

    [<Test>]
    member test.``sepBy`` () =
        let floatsListParser = sepBy pfloat (pstring "//")
        testp floatsListParser "1.0//3.0//5" |> should equal (Some([1.; 3.; 5.]))

    [<Test>]
    member test.``pipe`` () =
        let parser = pipe2 (pfloat .>> spaces) pfloat (fun a b -> if a > b then a else b)
        testp parser "1.0 2.0" |> should equal (Some(2.0))
        testp parser "3.0 2.0" |> should equal (Some(3.0))

//        let identifier =
//            let isIdentifierFirstChar c = isLetter c || c = '_'
//            let isIdentifierChar c = isLetter c || isDigit c || c = '_'
//
//            many1Satisfy2L isIdentifierFirstChar isIdentifierChar "identifier"
//                .>> ws // skips trailing whitepace
//        let stringLiteral =
//            let normalChar = satisfy (fun c -> c <> '\\' && c <> '"')
//            let unescape c = match c with
//                             | 'n' -> '\n'
//                             | 'r' -> '\r'
//                             | 't' -> '\t'
//                             | c   -> c
//            let escapedChar = pstring "\\" >>. (anyOf "\\nrt\"" |>> unescape)
//            between (pstring "\"") (pstring "\"")
//                    (manyChars (normalChar <|> escapedChar))
//
//        let stringConstant = pipe3 identifier (str_ws "=") stringLiteral
//                               (fun id _ str -> StringConstant(id, str))
//        testp stringConstant "myString = \"stringValue\""