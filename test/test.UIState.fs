module testUIState

open NUnit.Framework
open FsUnit
open UIState

[<TestFixture>] 
type ``Given UI state functions`` () =

    [<Test>] 
    member test.``When calling getAppState from beginning, the counts are 0`` () =
        printfn "When calling getAppState from beginning, the counts are 0"
        let getState, _, _, _, _, cexit = UIState.getUIStateFunctions ()
        
        getState() |> should equal { Active=0; ListLength=0; Filtered=0 }
        cexit() // cleanup
        
    [<Test>] 
    member test.``When calling Working, one active should be returned`` () =
        printfn "When calling Working, one active should be returned"
        let getState, _, addWorking, _, _, cexit = UIState.getUIStateFunctions ()
        
        addWorking()
        getState().Active |> should equal 1
        cexit() // cleanup
        
    [<Test>] 
    member test.``When calling Working and Done with the same amount, then Active should be 0``  () =
        printfn "When calling Working and Done with the same amount, then Active should be 0"
        let getState, _, addWorking, addDone, _, cexit = UIState.getUIStateFunctions ()
        
        seq { for i in 1..100 -> async { addWorking()
                                         do! Async.Sleep(50)
                                         addDone()}}
        |> Async.Parallel
        |> Async.RunSynchronously
        |> ignore
        
        getState().Active |> should equal 0
        cexit() // cleanup
        
    [<Test>] 
    member test.``Set state twice, then when getting state, last one is returned`` () =
        printfn "Set state twice, then when getting state, last one is returned"
        let getState, _, _, _, setCounts, cexit = UIState.getUIStateFunctions ()
        
        setCounts 1 2
        setCounts 3 4
        getState() |> should equal { Active=0; ListLength=3; Filtered=4 }
        cexit() // cleanup