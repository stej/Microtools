module UIState

open Utils 

type private MsgType = 
    | Working
    | Done
    | State of AsyncReplyChannel<int>
    | StrState of AsyncReplyChannel<string>

let getAppState, getAppStrState, addWorking, addDone =
    let mp = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop active = async {
                let! msg = mbox.Receive()
                ldbgp "UI state: {0}" active
                match msg with
                | Working           -> return! loop (active+1)
                | Done              -> return! loop (active-1)
                | State(channel)    -> channel.Reply(active)
                                       return! loop active
                | StrState(channel) -> match active with | 0 -> channel.Reply("Done")
                                                         | _ -> channel.Reply("Working")
                                       return! loop active
            }
            loop 0
        )
    (fun () -> mp.PostAndReply(fun channel -> State(channel))),
    (fun () -> mp.PostAndReply(fun channel -> StrState(channel))),
    (fun () -> mp.Post(Working)),
    (fun () -> mp.Post(Done))