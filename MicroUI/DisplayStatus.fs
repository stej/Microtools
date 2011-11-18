module DisplayStatus

open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media
open Status
open Utils
open WpfUtils

let OpacityFiltered, OpacityOld = 0.2, 0.5

let rec private convertToStatusDisplayInfo filterDoesntHideStatuses (filterer: statusInfo->bool) (statusInfo:statusInfo) : StatusInfoToDisplay =
    let rec convertToFilterInfo (children:StatusInfoToDisplay list) =
    
        let existsUnfilteredDescendant = 
            children |> List.exists (fun c -> not c.FilterInfo.Filtered || c.FilterInfo.HasUnfilteredDescendant)
        let hasSomeVisibleDescendant =
            existsUnfilteredDescendant || (filterDoesntHideStatuses && not(children.IsEmpty))

        { Filtered = filterer statusInfo
          HasUnfilteredDescendant = existsUnfilteredDescendant 
          HasSomeDescendantsToShow = hasSomeVisibleDescendant }

    let children = 
        statusInfo.Children 
        |> Seq.map (fun c -> convertToStatusDisplayInfo filterDoesntHideStatuses filterer c) 
        |> Seq.toList
    {
        StatusInfo = statusInfo
        Children = children
        FilterInfo = convertToFilterInfo children
        TextFragments = TextSplitter.splitText statusInfo.Status.Text
    }

module LitlePreview = 
    let private convertToPreviewSource sDisplayInfo =
        ({ ImageOpacity = if sDisplayInfo.FilterInfo.Filtered then OpacityFiltered else 1.0 },
         sDisplayInfo)

    // todo: run only needed parts (manipulating with controls) on UI thread.. 
    // currently all the stuff is supposed to run on UI thread
    let fillPictures (wrap:WrapPanel) statusFilterer showHiddenStatuses statuses =

        wrap.Children.Clear()
        let previewSources = 
            statuses 
            |> Seq.toList
            |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
            |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
            |> List.map (fst 
                         >> (convertToStatusDisplayInfo showHiddenStatuses statusFilterer)
                         >> convertToPreviewSource)

        previewSources 
            |> List.map WpfUtils.createLittlePicture
            |> List.iter (wrap.Children.Add >> ignore)

        previewSources |> List.map snd

module ConversationPreview = 
    type private StatusVisibilityDecider(showHiddenStatuses) =
        // status id of first status (for retweets it is status id of the retweet, not the original status)
        let _firstLogicalStatusId = 
            match PreviewsState.userStatusesState.GetFirstStatusId() with
            | Some(value) -> value
            | None -> 0L

        /// causes the filter that the status should not be displayed? (takes into account children as well)
        let statusIsNotShownDueToFilter (sDisplayInfo:WpfUtils.StatusInfoToDisplay) = 
            // dont' show hidden & is filtered & doesn't have unfiltered children
            not showHiddenStatuses && 
            sDisplayInfo.FilterInfo.Filtered &&
            not sDisplayInfo.FilterInfo.HasUnfilteredDescendant

        member x.isRootStatusVisible (sDisplayInfo:WpfUtils.StatusInfoToDisplay) =
            let filterHasNoEffectOnStatus = not (statusIsNotShownDueToFilter sDisplayInfo)
            let isOlderThanFirstRequestedStatus = sDisplayInfo.StatusInfo.Status.LogicalStatusId >= _firstLogicalStatusId
            
            if filterHasNoEffectOnStatus && isOlderThanFirstRequestedStatus then
                true
            else
                sDisplayInfo.FilterInfo.HasUnfilteredDescendant || // if is older then first or is filtered, show only if there are unfiltered children
                sDisplayInfo.FilterInfo.HasSomeDescendantsToShow   // or the descendants should be displayed (forced by showHiddenStatuses variable)

        // function that decides if the status in the conversation should be displayed
        member x.isStatusVisible = statusIsNotShownDueToFilter >> not
        member x.firstLogicalStatusId = _firstLogicalStatusId
    
//    let fillDetails window (details:StackPanel) statusFilterer showHiddenStatuses statuses =    
//        ldbg "UI: fillDetails"
//    
//        // status id of first status (for retweets it is status id of the retweet, not the original status)
//        let firstLogicalStatusId = 
//            match PreviewsState.userStatusesState.GetFirstStatusId() with
//            | Some(value) -> value
//            | None -> 0L
//
//        ldbgp "UI: fillDetails, first is {0}" firstLogicalStatusId
//        let visibilityDecider = new StatusVisibilityDecider(showHiddenStatuses, firstLogicalStatusId)
//
//        let setControlOpacity (detailCtl:WpfUtils.conversationNodeControlsInfo) =
//            if detailCtl.GetLogicalStatusId() < firstLogicalStatusId then
//                detailCtl.Detail.Opacity <- OpacityOld
//            if detailCtl.StatusToDisplay.FilterInfo.Filtered then
//                detailCtl.Detail.Opacity <- OpacityFiltered
//
//        let createConversation rootFilterInfo =
//            let controls = WpfUtils.createConversationControls WpfUtils.End details
//            WpfUtils.updateConversation controls visibilityDecider.isStatusVisible rootFilterInfo
//            |> Seq.map (doAndRet setControlOpacity)
//
//        details.Children.Clear()
//        statuses 
//          |> Seq.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
//          |> Seq.sortBy (fun (sInfo, displayDate) -> displayDate)
//          |> Seq.map (fst 
//                      >> (convertToStatusDisplayInfo showHiddenStatuses statusFilterer)
//                      >> convertToConversationSource)
//          //|> Seq.filter visibilityDecider.isRootStatusVisible
//          |> Seq.map (fun rootStatusDisplayInfo -> createConversation rootStatusDisplayInfo)
    let private convertToConversationSource (visibilityDecider:StatusVisibilityDecider) sRootDisplayInfo =
        let getControlOpacity sDisplayInfo =
            if sDisplayInfo.StatusInfo.Status.LogicalStatusId < visibilityDecider.firstLogicalStatusId then
                OpacityOld
            else if sDisplayInfo.FilterInfo.Filtered then
                OpacityFiltered
            else 1.0

        let ret = new ResizeArray<_>()
        let rec _convert depth sDisplayInfo = 
            ret.Add({ Depth = depth; Opacity = getControlOpacity sDisplayInfo }, sDisplayInfo)
            sDisplayInfo.Children
                |> Seq.filter (visibilityDecider.isStatusVisible)
                |> Seq.map (fun sInfo -> (sInfo, sInfo.StatusInfo.StatusId()))
                |> Seq.sortBy snd
                |> Seq.map fst
                |> Seq.iter (_convert (depth+1))
        ret |> Seq.toList
        

    let fillDetails window (details:StackPanel) statusFilterer showHiddenStatuses statuses =    
        ldbg "UI: fillDetails"
        let visibilityDecider = new StatusVisibilityDecider(showHiddenStatuses)

        let createConversation conversationRows =
            let controls = WpfUtils.createConversationControls WpfUtils.End details
            WpfUtils.updateConversation controls conversationRows

        details.Children.Clear()
        statuses 
          |> Seq.toList
          |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
          |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
          |> List.map (fst >> (convertToStatusDisplayInfo showHiddenStatuses statusFilterer))
          |> List.filter visibilityDecider.isRootStatusVisible
          |> List.map (convertToConversationSource visibilityDecider)
          //
          |> List.map (fun conversationRows -> createConversation conversationRows)