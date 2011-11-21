module ScriptingHelpers

open System
open System.Collections.Generic
open System.Xml
open Utils
open OAuth
open Status
open System.Windows.Threading
open ipy
open TwitterLimits
open TextSplitter
open DisplayStatus

open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Documents

type public Helpers (window, details:StackPanel, wrapContent:WrapPanel) = 
    let neverFilterer = fun _ -> false
    let fillDetails  = FullConversation.fill details
    let fillPictures = LitlePreview.fill wrapContent UIFilterDescriptor.NoFilter

    member x.loadTree status = StatusesReplies.loadSavedReplyTree status
    member x.show (statuses: statusInfo seq) = 
        WpfUtils.dispatchMessage window (fun _ -> fillPictures statuses |> ignore
                                                  fillDetails statuses |> ignore)

    // print and show status together
    member x.show (o: Object) = 
        match o with
        | :? statusInfo as sInfo-> 
            WpfUtils.dispatchMessage window (fun _ -> fillPictures [sInfo] |> ignore
                                                      fillDetails [sInfo] |> ignore)
        | _ -> 
            WpfUtils.dispatchMessage window (fun _ -> 
                wrapContent.Children.Clear()
                let ret = new TextBlock(TextWrapping = TextWrapping.Wrap,
                                Padding = new Thickness(0.),
                                Margin = new Thickness(5., 0., 0., 5.))
                ret.Inlines.Add(new Run(o.ToString()))
                wrapContent.Children.Add(ret) |> ignore
            )
    member x.find text =
        StatusDb.statusesDb.GetStatusesFromSql(sprintf "select * from Status where Text like '%%%s%%' or UserName like '%%%s%%'" text text)
    member x.exportToHtml (statuses: statusInfo seq) =
        let file = System.IO.Path.GetTempFileName().Replace(".tmp", ".html")
        let processText text =
            let convertPart part =
                let link, text = TextSplitter.urlFragmentToLinkAndName part
                match part with
                | FragmentWords(u)       -> u
                | _                      -> (sprintf "<a href=\"%s\">%s</a>" link text)
            let parts = 
                TextSplitter.splitText text
                |> Array.map convertPart
            String.Join("", parts)
        // h.exportToHtml(h.find('logger'))
        let rec processStatus depth status =
            let text = 
                let rawStatus = status.Status
                let user = rawStatus.UserName
                let img = rawStatus.UserProfileImage
                let statusid = rawStatus.StatusId
                sprintf "
                            <div class=\"status\" style=\"margin-left:%dem\">
                                <a href=\"http://twitter.com/%s\" class=\"img\">
                                    <img src=\"%s\" />
                                </a>
                                <div class=\"body\">
                                  <div class=\"meta\">
                                    <a href=\"http://twitter.com/%s/status/%d\">%s</a>
                                  </div>
                                  <span>%s</span>
                                </div>
                            </div>" (depth*3) user img user statusid (rawStatus.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm")) (processText rawStatus.Text)
            System.IO.File.AppendAllText(file, text)
            status.Children |> Seq.iter (processStatus (depth+1))
        System.IO.File.AppendAllText(file, "<html>
            <head>
                <meta http-equiv=\"content-type\" content=\"text/html; charset=utf-8\" />
                <style>
                    .status { font-family: Verdana; font-size: 10pt; color:#444; 
                              margin-bottom:.5em; 
                              padding-top:.3em;
                              padding-bottom:.3em;
                              width:45em; 
                              border-bottom:1px dashed gray }
                    .status img { width: 40px; height: 40px; display:inline-block; }
                    .status .body { width: 40em; display:inline-block; vertical-align:top }
                    .status .meta { font-size: smaller; color: #999; font-style:italic }
                    .status .meta a, .status a.img { text-decoration: none }
                </style>
            </head>
            <body>")
        statuses |> Seq.iter (processStatus 0)
        System.IO.File.AppendAllText(file, "</body>
        </html>")
        System.Diagnostics.Process.Start(file)
    member x.DownloadAndSavePersonalStatuses() = 
        let statusesState = new PreviewsState.UserStatusesState();
        [async { let! statuses = Twitter.PersonalStatuses.friendsChecker.Check()
                 Twitter.PersonalStatuses.saveStatuses Twitter.FriendsStatuses statuses
                 return statuses }
         async { let! statuses = Twitter.PersonalStatuses.mentionsChecker.Check()
                 Twitter.PersonalStatuses.saveStatuses Twitter.MentionsStatuses statuses
                 return statuses }
         //async { let! statuses = Twitter.PersonalStatuses.retweetsChecker.Check()
         //        Twitter.PersonalStatuses.saveStatuses Twitter.RetweetsStatuses statuses
         //        return statuses }
        ]
        |> Async.Parallel
        |> Async.RunSynchronously
        |> Array.choose Operators.id
        |> Array.iter statusesState.AddStatuses
        |> ignore
        printf "Waiting until all statuses are saved.."
        DbInterface.dbAccess.GetLastTimelineId() |> ignore //ensure waiting until all statuses are saved
        printfn "done"
        statusesState.GetStatuses() |> fst
    member x.LoadConversations(maxConversations) = StatusDb.statusesDb.GetRootStatusesHavingReplies(maxConversations)
    member x.LoadChildren(status) = StatusesReplies.loadSavedReplyTree(status)
    member x.LoadLastStatuses(maxStatuses) = StatusDb.statusesDb.GetTimelineStatusesBefore(maxStatuses, Int64.MaxValue)
    member x.RootConversationsWithNoDownload(baseStatuses, toRoot) = StatusesReplies.rootConversationsWithNoDownload baseStatuses toRoot
    member x.RootConversationsWithDownload(baseStatuses, toRoot) = StatusesReplies.rootConversationsWithDownload baseStatuses toRoot