module UIState

open Utils 

type UIStateDescriptor = {
    Active : int
    ListLength : int
    Filtered : int
}

type private MsgType = 
    | Working
    | Done
    | State of AsyncReplyChannel<UIStateDescriptor>
    | StrState of AsyncReplyChannel<string>
    | Counts of int * int

let getAppState, getAppStrState, addWorking, addDone, setCounts =
    let mp = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop active listLen filtered = async {
                let! msg = mbox.Receive()
                ldbgp "UI state: {0}" active
                match msg with
                | Working           -> return! loop (active+1) listLen filtered
                | Done              -> return! loop (active-1) listLen filtered
                | State(channel)    -> channel.Reply({ Active = active; ListLength = listLen; Filtered = filtered })
                                       return! loop active listLen filtered
                | StrState(channel) -> let s = match active with | 0 -> "Done"
                                                                 | _ -> "Working"
                                       let ret = sprintf "%s.. Count: %d/%d" s listLen filtered
                                       channel.Reply(ret)
                                       return! loop active listLen filtered
                | Counts(newlen, newfiltered) -> 
                                       return! loop active newlen newfiltered
            }
            loop 0 0 0
        )
    (fun () -> mp.PostAndReply(fun channel -> State(channel))),
    (fun () -> mp.PostAndReply(fun channel -> StrState(channel))),
    (fun () -> mp.Post(Working)),
    (fun () -> mp.Post(Done)),
    (fun len filtered -> mp.Post(Counts(len, filtered)))