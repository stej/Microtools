module DisplayStatus

open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media
open Utils

let OpacityFiltered, OpacityOld = 0.2, 0.5

let fillPictures (wrap:WrapPanel) statusFilterer showHiddenStatuses statuses =
    let setControlOpacity (filterInfo:WpfUtils.StatusInfoToDisplay) (pic:Image) = 
        if filterInfo.Filtered then pic.Opacity <- OpacityFiltered

    wrap.Children.Clear()
    statuses 
      |> Seq.toList
      |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
      |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
      |> List.map (fst >> (WpfUtils.convertToFilterInfo showHiddenStatuses statusFilterer))
      |> List.map (fun sFilterInfo -> sFilterInfo, WpfUtils.createLittlePicture sFilterInfo.StatusInfo.Status) 
      |> List.map (fun (sFilterInfo,pic) -> wrap.Children.Add(pic) |> ignore
                                            setControlOpacity sFilterInfo pic
                                            sFilterInfo)

type private StatusVisibilityDecider(showHiddenStatuses, firstLogicalStatusId) =
    /// causes the filter that the status should not be displayed? (takes into account children as well)
    let statusIsNotShownDueToFilter (filterInfo:WpfUtils.StatusInfoToDisplay) = 
        // dont' show hidden & is filtered & doesn't have unfiltered children
        not showHiddenStatuses && 
        filterInfo.Filtered &&
        not filterInfo.HasUnfilteredDescendant

    member x.isRootStatusVisible (filterInfo:WpfUtils.StatusInfoToDisplay) =
        let filterHasNoEffectOnStatus = not (statusIsNotShownDueToFilter filterInfo)
        let isOlderThanFirstRequestedStatus = filterInfo.StatusInfo.Status.LogicalStatusId >= firstLogicalStatusId
            
        if filterHasNoEffectOnStatus && isOlderThanFirstRequestedStatus then
            true
        else
            filterInfo.HasUnfilteredDescendant || // if is older then first or is filtered, show only if there are unfiltered children
            filterInfo.HasSomeDescendantsToShow   // or the descendants should be displayed (forced by showHiddenStatuses variable)

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
        if detailCtl.StatusInfo.Status.LogicalStatusId < firstLogicalStatusId then
            detailCtl.Detail.Opacity <- OpacityOld
        if detailCtl.Filtered then
            detailCtl.Detail.Opacity <- OpacityFiltered

    let updateConversation rootFilterInfo =
        let controls = WpfUtils.createConversationControls WpfUtils.End details
        WpfUtils.setNewConversation controls visibilityDecider.isStatusVisible rootFilterInfo
        |> Seq.iter setControlOpacity

    details.Children.Clear()
    statuses 
      |> Seq.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
      |> Seq.sortBy (fun (sInfo, displayDate) -> displayDate)
      |> Seq.map (fst >> (WpfUtils.convertToFilterInfo showHiddenStatuses statusFilterer))
      |> Seq.filter visibilityDecider.isRootStatusVisible
      |> Seq.iter (fun rootFilterInfo -> WpfUtils.dispatchMessage window (fun _ -> updateConversation rootFilterInfo))
    ldbg "UI: fillDetails done"