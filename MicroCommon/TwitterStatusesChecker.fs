module TwitterStatusesChecker

open System
open System.Xml
open Status
open Utils
open TwitterLimits

type TwitterStatusesCheckerMessages =
| Stop
| CheckForStatuses of AsyncReplyChannel<(statusInfo list) option>

type Checker(checkerType, statusNodeConvertor:XmlNode->statusInfo option, getUrl, isItSaveToQuery) =
    let extractStatuses statusesXml =
        statusesXml
            |> xpathNodes "//statuses/status"
            |> Seq.cast<XmlNode> 
            |> Seq.map statusNodeConvertor
            |> Seq.filter (fun s -> s.IsSome)
            |> Seq.map (fun s -> s.Value)
    let mbox =
        MailboxProcessor.Start(fun mbox ->
            let rec loop () = async {
                let! msg = mbox.Receive()
                ldbgp2 "TwitterStatusesChecker {0} message: {1}" checkerType msg
                match msg with
                | Stop -> ()
                | CheckForStatuses(chnl) ->
                    if isItSaveToQuery() then
                        let url = getUrl()
                        linfop "Check {0}" url
                        let xml = new XmlDocument()
                        match OAuthInterface.oAuthAccess.requestTwitter url with
                         | None ->
                            chnl.Reply(None)
                         | Some(sentXml, statusCode, headers) ->
                            twitterLimits.UpdateSearchLimitFromResponse(statusCode, headers)
                            match sentXml with
                            | ""   -> xml.LoadXml("<statuses type=\"array\"></statuses>")
                            | text -> xml.LoadXml(text)
                            let statuses = xml |> extractStatuses
                                               |> Seq.toList
                        
                            chnl.Reply(Some(statuses))

                        linfop2 "Check {0} - {1} done" checkerType url
                    else
                        chnl.Reply(None)
                        linfop "Twitter limits exceeded. Type: {0}" checkerType

                    return! loop()
            }
            loop()
        )
    do
        mbox.Error.Add(fun exn -> lerrex exn (sprintf "Error in TwitterStatusesChecker %A" checkerType) )
    member x.Check() = mbox.PostAndAsyncReply(CheckForStatuses)
    member x.Stop() = mbox.Post(Stop)