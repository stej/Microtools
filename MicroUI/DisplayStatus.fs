module DisplayStatus

open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media
open Utils

let OpacityFiltered, OpacityOld = 0.2, 0.5

// todo: run only needed parts (manipulating with controls) on UI thread.. 
// currently all the stuff is supposed to run on UI thread
let fillPictures (wrap:WrapPanel) statusFilterer showHiddenStatuses statuses =
    let setControlOpacity (sDisplayInfo:WpfUtils.StatusInfoToDisplay) (pic:Image) = 
        if sDisplayInfo.FilterInfo.Filtered then pic.Opacity <- OpacityFiltered

    wrap.Children.Clear()
    statuses 
      |> Seq.toList
      |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
      |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
      |> List.map (fst >> (WpfUtils.convertToStatusDisplayInfo showHiddenStatuses statusFilterer))
      |> List.map (fun sDisplayInfo -> sDisplayInfo, WpfUtils.createLittlePicture sDisplayInfo)
      |> List.map (fun (sDisplayInfo,pic) -> wrap.Children.Add(pic) |> ignore
                                             setControlOpacity sDisplayInfo pic
                                             sDisplayInfo)

type private StatusVisibilityDecider(showHiddenStatuses, firstLogicalStatusId) =
    /// causes the filter that the status should not be displayed? (takes into account children as well)
    let statusIsNotShownDueToFilter (sDisplayInfo:WpfUtils.StatusInfoToDisplay) = 
        // dont' show hidden & is filtered & doesn't have unfiltered children
        not showHiddenStatuses && 
        sDisplayInfo.FilterInfo.Filtered &&
        not sDisplayInfo.FilterInfo.HasUnfilteredDescendant

    member x.isRootStatusVisible (sDisplayInfo:WpfUtils.StatusInfoToDisplay) =
        let filterHasNoEffectOnStatus = not (statusIsNotShownDueToFilter sDisplayInfo)
        let isOlderThanFirstRequestedStatus = sDisplayInfo.StatusInfo.Status.LogicalStatusId >= firstLogicalStatusId
            
        if filterHasNoEffectOnStatus && isOlderThanFirstRequestedStatus then
            true
        else
            sDisplayInfo.FilterInfo.HasUnfilteredDescendant || // if is older then first or is filtered, show only if there are unfiltered children
            sDisplayInfo.FilterInfo.HasSomeDescendantsToShow   // or the descendants should be displayed (forced by showHiddenStatuses variable)

    // function that decides if the status in the conversation should be displayed
    member x.isStatusVisible = statusIsNotShownDueToFilter >> not
    
let fillDetails window (details:StackPanel) statusFilterer showHiddenStatuses statuses =    
    ldbg "UI: fillDetails"
    
    // status id of first status (for retweets it is status id of the retweet, not the original status)
    let firstLogicalStatusId = 
        match PreviewsState.userStatusesState.GetFirstStatusId() with
        | Some(value) -> value
        | None -> 0L
    ldbgp "UI: fillDetails, first is {0}" firstLogicalStatusId
    let visibilityDecider = new StatusVisibilityDecider(showHiddenStatuses, firstLogicalStatusId)

    let setControlOpacity (detailCtl:WpfUtils.conversationNodeControlsInfo) =
        if detailCtl.GetLogicalStatusId() < firstLogicalStatusId then
            detailCtl.Detail.Opacity <- OpacityOld
        if detailCtl.StatusToDisplay.FilterInfo.Filtered then
            detailCtl.Detail.Opacity <- OpacityFiltered

    let createConversation rootFilterInfo =
        let controls = WpfUtils.createConversationControls WpfUtils.End details
        WpfUtils.updateConversation controls visibilityDecider.isStatusVisible rootFilterInfo
        |> Seq.map (doAndRet setControlOpacity)

    details.Children.Clear()
    statuses 
      |> Seq.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
      |> Seq.sortBy (fun (sInfo, displayDate) -> displayDate)
      |> Seq.map (fst >> (WpfUtils.convertToStatusDisplayInfo showHiddenStatuses statusFilterer))
      |> Seq.filter visibilityDecider.isRootStatusVisible
      |> Seq.map (fun rootStatusDisplayInfo -> createConversation rootStatusDisplayInfo)