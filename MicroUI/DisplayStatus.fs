module DisplayStatus

open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media

let fillPictures (wrap:WrapPanel) statuses =
    wrap.Children.Clear()
    statuses 
      |> Seq.map (fun status -> (status, StatusFunctions.GetNewestDisplayDateFromConversation status))
      |> Seq.sortBy (fun (status, displayDate) -> displayDate)
      |> Seq.map fst
      |> Seq.map (fun status -> WpfUtils.createLittlePicture status) 
      |> Seq.iter (fun pic -> wrap.Children.Add(pic) |> ignore)

let fillDetails window (details:StackPanel) filterText statuses =
    let filter = StatusFilter.parseFilter filterText

    // status id of first status (for retweets it is status id of the retweet, not the original status)
    let firstLogicalStatusId = 
        match PreviewsState.userStatusesState.GetFirstStatusId() with
        | Some(value) -> value
        | None -> 0L
    let showStatus rootStatus =
        let controls = WpfUtils.createConversationControls WpfUtils.End details
        WpfUtils.setNewConversation controls rootStatus
        |> Seq.iter (fun detailCtl ->   //conversationNodeControlsInfo
                        if detailCtl.Status.LogicalStatusId < firstLogicalStatusId then
                            detailCtl.Detail.Opacity <- 0.5
                        if StatusFilter.matchesFilter filter detailCtl.Status then
                            detailCtl.Detail.Opacity <- 0.2
                     )
    details.Children.Clear()
    statuses 
      |> Seq.map (fun status -> (status, StatusFunctions.GetNewestDisplayDateFromConversation status))
      |> Seq.sortBy (fun (status, displayDate) -> displayDate)
      |> Seq.map fst
      |> Seq.iter (fun rootStatus -> WpfUtils.dispatchMessage window (fun f -> showStatus rootStatus))