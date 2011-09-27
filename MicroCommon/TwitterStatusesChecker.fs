module TwitterStatusesChecker

open System
open System.Xml
open Status
open Utils
open DbFunctions

type TwitterStatusesCheckerMessages =
| Stop
| CheckForStatuses of AsyncReplyChannel<(XmlDocument * Net.HttpStatusCode * Net.WebHeaderCollection) option>

type Checker(checkerName, getUrl) =
    let mbox = 
        MailboxProcessor.Start(fun mbox ->
            let rec loop () = async {
                let! msg = mbox.Receive()
                ldbgp2 "TwitterStatusesChecker {0} message: {1}" checkerName msg
                match msg with
                | Stop -> ()
                | CheckForStatuses(chnl) ->
                    let url = getUrl()
                    linfop "Check {0}" url
                    let xml = new XmlDocument()
                    match OAuth.requestTwitter url with
                     | None ->
                        chnl.Reply(None)
                     | Some("", statusCode, headers) ->
                        xml.LoadXml("<statuses type=\"array\"></statuses>")
                        chnl.Reply((xml, statusCode, headers)|>Some)
                     | Some(text, statusCode, headers) ->
                        xml.LoadXml(text)
                        chnl.Reply((xml, statusCode, headers)|>Some)
                    linfop2 "Check {0} - {1} done" checkerName url

                    return! loop()
            }
            loop()
        )
    do
        mbox.Error.Add(fun exn -> lerrex exn (sprintf "Error in TwitterStatusesChecker %s" checkerName) )
    member x.Check() = mbox.PostAndAsyncReply(CheckForStatuses)
    member x.Stop() = mbox.Post(Stop)