module test.subscriptionsConfig

open FsUnit
open NUnit.Framework
open SubscriptionsConfig

type ``Test subscriptions parser`` ()=

    [<Test>]
    member test.``empty string returns empty list`` () =
        parseSubscriptionsFromString "" |> should equal [||]

    [<Test>]
    member test.``unknown string is properly handled`` () =
        parseSubscriptionsFromString "abc" |> should equal [|Unknown("abc")|]

    [<Test>]
    member test.``parse basic`` () =
        parseSubscriptionsFromString "mentions" |> should equal [|Mentions|]
        parseSubscriptionsFromString "Mentions" |> should equal [|Mentions|]

        parseSubscriptionsFromString "timeline" |> should equal [|Timeline|]
        parseSubscriptionsFromString "Timeline" |> should equal [|Timeline|]

    [<Test>]
    member test.``parse one list without id returns Unknown`` () =
        parseSubscriptionsFromString "list " |> should equal [|Unknown("list")|]

    [<Test>]
    member test.``parse one list`` () =
        parseSubscriptionsFromString "list 123" |> should equal [|ListSubscription(123L)|]

    [<Test>]
    member test.``parse one list with bad id`` () =
        parseSubscriptionsFromString "list 123a" |> should equal [|Unknown("list 123a")|]
        parseSubscriptionsFromString "list a123" |> should equal [|Unknown("list a123")|]

    [<Test>]
    member test.``parse more items`` () =
        parseSubscriptionsFromString "list 123; list 453" |> should equal [|ListSubscription(123L); ListSubscription(453L)|]
        parseSubscriptionsFromString "  Timeline; mentions; " |> should equal [|Timeline; Mentions|]
        parseSubscriptionsFromString "  list 45; Timeline; mentions; " |> should equal [|ListSubscription(45L); Timeline; Mentions|]

    [<Test>]
    member test.``parse more items with invalid list`` () =
        parseSubscriptionsFromString "Timeline; list a123; list 453" |> should equal [|Timeline; Unknown("list a123"); ListSubscription(453L)|]