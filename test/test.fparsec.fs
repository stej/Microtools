module test.fparsec

open FParsec
open FsUnit
open NUnit.Framework
open System

type StringConstant = StringConstant of string * string

type FilterItem = 
    | Regex of string
    | StatusText of string
    | User of string
    | UserRetweet of string
    | UserTimeline of string
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
        let charList2String (cl:char list) =
            cl |> List.map string
               |> String.concat ""
        let baseLetters c = 
            isLetter c || isDigit c || c = '_' || c = '-'
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
//            let simpleText   = many1Satisfy (fun c -> c <> ' ')             |>> StatusText
            let simpleText   = pipe2 (satisfy baseLetters) 
                                 (many (noneOf " #"))
                                 (fun a b -> a.ToString()+(charList2String b)) |>> StatusText
            let textWithSpace= stringInApostrophes                          |>> StatusText
            let allTimeline  = pstring "timeline@all"                       |>> ignore |>> (fun _ -> AllTimeline)
            let allRetweets  = pstring "rt@all"                             |>> ignore |>> (fun _ -> AllRetweets)
            let user         = pstring "@"        >>. many1Satisfy baseLetters |>> User
            let userRetweet  = pstring "rt@"      >>. many1Satisfy baseLetters |>> UserRetweet
            let userTimeline = pstring "timeline@">>. many1Satisfy baseLetters |>> UserTimeline
            let filterRef    = pstring "#f:"      >>. many1Satisfy baseLetters |>> FilterReference
            let parsers = 
                choice [allRetweets
                        allTimeline
                        userRetweet
                        userTimeline
                        regex
                        stringInApostrophes |>> StatusText
                        simpleText
                        user
                        filterRef]
            spaces >>. (sepBy parsers (skipAnyOf " ")) .>> spaces .>> eof

        testp filterParser "#r:'abc'"       |> should equal (Some([Regex("abc")]))
        testp filterParser "#r:    'abc'"   |> should equal (Some([Regex("abc")]))
        testp filterParser "#r:'(a'"        |> should equal (Some([Regex("(a")]))
        testp filterParser @"#r:'\n\t\'x'"  |> should equal (Some([Regex("\\n\\t'x")]))
    
        testp filterParser @"'\n\t\'x'"  |> should equal (Some([StatusText("\\n\\t'x")]))
        testp filterParser "'a b c'"     |> should equal (Some([StatusText("a b c")]))
        testp filterParser "abc"         |> should equal (Some([StatusText("abc")]))
        //testp filterParser "abd "        |> should equal (Some([StatusText("abd")]))
        testp filterParser " abe"        |> should equal (Some([StatusText("abe")]))

        testp filterParser "@userx"        |> should equal (Some([User("userx")]))
        testp filterParser "@"             |> should equal None
        testp filterParser "rt@all"        |> should equal (Some([AllRetweets]))
        testp filterParser "rt@userx"      |> should equal (Some([UserRetweet("userx")]))
        testp filterParser "rt@"           |> should equal None
        testp filterParser "timeline@usax" |> should equal (Some([UserTimeline("usax")]))
        testp filterParser "timeline@all"  |> should equal (Some([AllTimeline]))

        testp filterParser "#f:filter-ref_x"  |> should equal (Some([FilterReference("filter-ref_x")]))
        testp filterParser "#f:"              |> should equal None
        testp filterParser "#f"               |> should equal None

        testp filterParser "#"                |> should equal None

        testp filterParser "@userx a-_$%( abc #r:'neco' #r:'\\d+necox \\t \\' abc\\d+' timeline@ab timeline@all rt@all rt@userxyz #f:f1 #f:f2_v2-0"  
        |> should equal (Some([User("userx")
                               StatusText("a-_$%(")
                               StatusText("abc")
                               Regex("neco")
                               Regex("\\d+necox \\t ' abc\\d+")
                               UserTimeline("ab")
                               AllTimeline
                               AllRetweets
                               UserRetweet("userxyz")
                               FilterReference("f1")
                               FilterReference("f2_v2-0")
                              ]))
//        testp filterParser "filter-common rt@keff85 " 
//        |> should equal (Some([StatusText("filter-common")
//                               UserRetweet("keff85")]))

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