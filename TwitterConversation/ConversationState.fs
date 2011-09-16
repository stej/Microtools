module ConversationState

open System
open Status
open Utils
  
type ConversationStateMessages =
| AddConversation of statusInfo
| UpdateConversation of statusInfo
| ContainsStatus of Int64 * AsyncReplyChannel<bool>
| GetConversations of AsyncReplyChannel<statusInfo list>
| GetConversation of Int64 * AsyncReplyChannel<statusInfo>
| RemoveConversation of statusInfo

type ConversationsState() =
    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop statuses = async {
                let! msg = mbox.Receive()
                ldbgp "Conversation state message: {0}" msg
                match msg with
                | AddConversation(conversationRoot) ->
                    ldbgp "Added conversation with root {0}" conversationRoot.Status.StatusId
                    return! loop(statuses @ [conversationRoot])
                | UpdateConversation(conversationRoot) ->
                    ldbgp "Updating conversation of root {0}" conversationRoot.Status.StatusId
                    let newList = 
                        statuses |> List.map (fun sInfo -> if sInfo.Status.StatusId = conversationRoot.Status.StatusId 
                                                           then conversationRoot
                                                           else sInfo)
                    return! loop(newList)
                | ContainsStatus(statusId, chnl) -> 
                    let contains = statuses |> List.exists (fun s -> s.Status.StatusId = statusId)
                    chnl.Reply(contains)
                    return! loop(statuses)
                | RemoveConversation(conversationRoot) ->
                    let stnew = statuses |> List.filter (fun sInfo -> sInfo.Status.StatusId <> conversationRoot.Status.StatusId)
                    return! loop(stnew)
                | GetConversations(chnl) ->
                    chnl.Reply(statuses |> Seq.toList)
                    return! loop(statuses)
                | GetConversation(id, chnl) ->
                    let conversationRoot = statuses |> List.find (fun status -> status.Status.StatusId = id)
                    chnl.Reply(conversationRoot)
                    return! loop(statuses) }
            ldbg "Starting Conversation state"
            loop([])
        )
    do
        mbox.Error.Add(fun exn ->
            (*match exn with
            | :? System.TimeoutException as exn -> ...
            | _ -> printfn "Unknown exception.")*)
            lerrex exn "Conversation state error")
    member x.AddConversation(s) = mbox.Post(AddConversation(s)); s
    member x.UpdateConversation(s) = mbox.Post(UpdateConversation(s)); s
    member x.RemoveConversation(s) = mbox.Post(RemoveConversation(s))
    member x.GetConversations() = mbox.PostAndReply(GetConversations)
    member x.GetConversation(statusId) = mbox.PostAndReply(fun reply -> GetConversation(statusId, reply))
    member x.ContainsStatus(statusId) = mbox.PostAndReply(fun reply -> ContainsStatus(statusId, reply))

    member x.AsyncGetConversations() = mbox.PostAndAsyncReply(GetConversations)
    member x.AsyncContainsStatus(statusId) = mbox.PostAndAsyncReply(fun reply -> ContainsStatus(statusId, reply))

let conversationsState = new ConversationsState()