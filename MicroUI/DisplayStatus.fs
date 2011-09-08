module DisplayStatus

open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media

let fillPictures (wrap:WrapPanel) statuses =
    wrap.Children.Clear()
    statuses 
      |> Seq.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
      |> Seq.sortBy (fun (sInfo, displayDate) -> displayDate)
      |> Seq.map fst
      |> Seq.map (fun sInfo -> WpfUtils.createLittlePicture sInfo.Status) 
      |> Seq.iter (fun pic -> wrap.Children.Add(pic) |> ignore)

    
let fillDetails window (details:StackPanel) filterText showHiddenStatuses statuses =
    // status id of first status (for retweets it is status id of the retweet, not the original status)
    let firstLogicalStatusId = 
        match PreviewsState.userStatusesState.GetFirstStatusId() with
        | Some(value) -> value
        | None -> 0L

    let statusFilterer = 
        let parsed = StatusFilter.parseFilter filterText
        StatusFilter.matchesFilter parsed

    /// causes the filter that the status should not be displayed? (takes into account children as well)
    let isNotShownDueToFilter (filterInfo:WpfUtils.StatusInfoToDisplay) = 
        // dont' show hidden & is filtered & doesn't have unfiltered children
        not showHiddenStatuses && 
        filterInfo.Filtered &&
        not filterInfo.HasUnfilteredDescendant

    let isRootStatusVisible (filterInfo:WpfUtils.StatusInfoToDisplay) =
        let filterHasNoEffectOnStatus = not (isNotShownDueToFilter filterInfo)
        let isOlderThanFirstRequestedStatus = filterInfo.StatusInfo.Status.LogicalStatusId >= firstLogicalStatusId
            
        if filterHasNoEffectOnStatus && isOlderThanFirstRequestedStatus then
            true
        else
            filterInfo.HasUnfilteredDescendant || // if is older then first or is filtered, show only if there are unfiltered children
            filterInfo.HasSomeDescendantsToShow   // or the descendants should be displayed (forced by showHiddenStatuses variable)

    // function that decides if the status in the conversation should be displayed
    let isStatusVisible = isNotShownDueToFilter >> not

    let setControlColor (detailCtl:WpfUtils.conversationNodeControlsInfo) =
        if detailCtl.StatusInfo.Status.LogicalStatusId < firstLogicalStatusId then
            detailCtl.Detail.Opacity <- 0.5
        if detailCtl.Filtered then
            detailCtl.Detail.Opacity <- 0.2

    let updateConversation rootFilterInfo =
        let controls = WpfUtils.createConversationControls WpfUtils.End details
        WpfUtils.setNewConversation controls isStatusVisible rootFilterInfo
        |> Seq.iter setControlColor

    details.Children.Clear()
    statuses 
      |> Seq.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
      |> Seq.sortBy (fun (sInfo, displayDate) -> displayDate)
      |> Seq.map (fst >> (WpfUtils.convertToFilterInfo showHiddenStatuses statusFilterer))
      |> Seq.filter isRootStatusVisible
      |> Seq.iter (fun rootFilterInfo -> WpfUtils.dispatchMessage window (fun f -> updateConversation rootFilterInfo))