module ScriptingHelpers

open System
open System.Collections.Generic
open System.Xml
open Utils
open OAuth
open Status
open System.Windows.Threading
open ipy

open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Documents

type public Helpers (window, details:StackPanel, wrapContent:WrapPanel) = 
    let fillDetails statuses = DisplayStatus.fillDetails window details "" statuses
    let fillPictures = DisplayStatus.fillPictures wrapContent

    member x.loadTree status = StatusesReplies.loadSavedReplyTree status
    member x.show (statuses: status seq) = 
        WpfUtils.dispatchMessage window (fun _ -> fillPictures statuses
                                                  fillDetails statuses)

    // print and show status together
    member x.show (o: Object) = 
        match o with
        | :? status as status -> 
            WpfUtils.dispatchMessage window (fun _ -> fillPictures [status]
                                                      fillDetails [status])
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
    member x.exportToHtml (statuses: status seq) =
        let file = System.IO.Path.GetTempFileName().Replace(".tmp", ".html")
        let processText text =
            let parts = seq { 
                for part in WpfUtils.regexUrl.Split(text) do
                    if WpfUtils.regexUrl.IsMatch(part) then
                        let matchGroups = WpfUtils.regexUrl.Match(part).Groups
                        if matchGroups.["url"].Success then yield! (sprintf "<a href=\"%s\">%s</a>" part part)
                        else if matchGroups.["user"].Success then yield! (sprintf "<a href=\"http://twitter.com/%s\">%s</a>" (part.TrimStart('@')) part)
                    else
                        yield! part
            }
            String.Join("", parts)
        let rec processStatus depth status =
            let text = sprintf "
                            <div class=\"status\" style=\"margin-left:%dem\">
                                <img src=\"%s\" />
                                <div class=\"body\">
                                  <div class=\"meta\">
                                    <a href=\"http://twitter.com/%s/status/%d\">%d</a>
                                    %s
                                  </div>
                                  <span>%s</span>
                                </div>
                            </div>" (depth*3) status.UserProfileImage status.UserName status.StatusId status.StatusId (status.Date.ToString("yyyy-MM-dd HH:mm")) (processText status.Text)
            System.IO.File.AppendAllText(file, text)
            status.Children |> Seq.iter (processStatus (depth+1))
        System.IO.File.AppendAllText(file, "<html>
            <head>
                <meta http-equiv=\"content-type\" content=\"text/html; charset=utf-8\" />
                <style>
                    .status { font-family: Verdana; font-size: 10pt; color:#444; margin-bottom:.5em }
                    .status img { width: 40px; height: 40px; display:inline-block; }
                    .status .body { width: 40em; display:inline-block; vertical-align:top }
                    .status .meta { font-size: smaller; color: #999; font-style:italic }
                    .status .meta a { text-decoration: none }
                </style>
            </head>
            <body>")
        statuses |> Seq.iter (processStatus 0)
        System.IO.File.AppendAllText(file, "</body>
        </html>")
        System.Diagnostics.Process.Start(file)
    member x.DownloadAndSavePersonalStatuses() = 
        Twitter.getLastStoredIds()
            |> Twitter.loadAndSaveNewPersonalStatuses
            |> Twitter.saveDownloadedStatuses
            |> fun downloaded -> downloaded.NewStatuses
            |> List.map (fun (status,_) -> status)
    member x.LoadConversations(maxConversations) = StatusDb.statusesDb.GetRootStatusesHavingReplies(maxConversations)
    member x.LoadChildren(status) = StatusesReplies.loadSavedReplyTree(status)
    member x.LoadLastStatuses(maxStatuses) = StatusDb.statusesDb.GetTimelineStatusesBefore(maxStatuses, Int64.MaxValue)
    member x.RootConversationsWithNoDownload(baseStatuses, toRoot) = StatusesReplies.rootConversationsWithNoDownload baseStatuses toRoot
    member x.RootConversationsWithDownload(baseStatuses, toRoot) = StatusesReplies.rootConversationsWithDownload baseStatuses toRoot