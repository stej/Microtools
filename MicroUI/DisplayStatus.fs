module DisplayStatus

open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media
open Status
open Utils
open WpfUtils

let OpacityFiltered, OpacityOld, OpacityVisible = 0.2, 0.5, 1.0

type UIFilterDescriptor = 
    { ShowHidden : bool 
      FilterOutRule : statusInfo -> bool
    } 
    with static member NoFilter = { ShowHidden = true; FilterOutRule = fun _ -> false }

let rec private convertToStatusDisplayInfo filter (statusInfo:statusInfo) : StatusInfoToDisplay =
    let rec convertToFilterInfo (children:StatusInfoToDisplay list) =
    
        let existsUnfilteredDescendant = 
            children |> List.exists (fun c -> not c.FilterInfo.Filtered || c.FilterInfo.HasUnfilteredDescendant)
        let hasSomeVisibleDescendant =
            existsUnfilteredDescendant || (filter.ShowHidden && not(children.IsEmpty))

        { Filtered = filter.FilterOutRule statusInfo
          HasUnfilteredDescendant = existsUnfilteredDescendant 
          HasSomeDescendantsToShow = hasSomeVisibleDescendant }

    let children = 
        statusInfo.Children 
        |> Seq.map (fun c -> convertToStatusDisplayInfo filter c) 
        |> Seq.toList
    {
        StatusInfo = statusInfo
        Children = children
        FilterInfo = convertToFilterInfo children
        TextFragments = TextSplitter.splitText statusInfo.Status.Text
    }

module LitlePreview = 
    let private convertToPreviewSource sDisplayInfo =
        ({ ImageOpacity = if sDisplayInfo.FilterInfo.Filtered then OpacityFiltered else OpacityVisible },
         sDisplayInfo)

    // todo: run only needed parts (manipulating with controls) on UI thread.. 
    // currently all the stuff is supposed to run on UI thread
    let fill (wrap:WrapPanel) (filter:UIFilterDescriptor) statuses =

        wrap.Children.Clear()
        let previewSources = 
            statuses 
            |> Seq.toList
            |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
            |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
            |> List.map (fst 
                         >> (convertToStatusDisplayInfo filter)
                         >> convertToPreviewSource)

        previewSources 
            |> List.map WpfUtils.createLittlePicture
            |> List.iter (wrap.Children.Add >> ignore)

        previewSources |> List.map snd

module private CommonConversationHelpers = 
    /// Helper type used in situation when conversations should be used and it should be decided whether
    /// the given node (in conversation) should be shown or not (depends also on children)
    type ConversationStatusVisibilityDecider(showHiddenStatuses) =
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

    type OpacityDecider = 
        { F : StatusInfoToDisplay -> float }
        with static member AlwaysVisible = { F = fun _ -> OpacityVisible }
    type StatusVisibilityDecider = 
        { F : StatusInfoToDisplay -> bool }
        with static member AlwaysVisible = { F = fun _ -> false }
    type BackgroundColorDecider =
        { F : StatusInfoToDisplay list -> StatusInfoToDisplay -> SolidColorBrush }
        with static member DefaultColor = { F = fun _ _ -> Brushes.White }
                            

    let convertToConversationSource (opacityDecider:OpacityDecider) (visibilityDecider:StatusVisibilityDecider) 
                                                                    (colorDecider: BackgroundColorDecider) sRootDisplayInfo =
        let ret = new ResizeArray<_>()
        let rec _convert depth parents sDisplayInfo = 
            ret.Add({ Depth = depth
                      Opacity = opacityDecider.F sDisplayInfo
                      BackgroundColor = colorDecider.F parents sRootDisplayInfo }, 
                    sDisplayInfo)
            sDisplayInfo.Children
                |> Seq.filter visibilityDecider.F
                |> Seq.map (fun sInfo -> (sInfo, sInfo.StatusInfo.StatusId()))
                |> Seq.sortBy snd
                |> Seq.map fst
                |> Seq.iter (_convert (depth+1) (sDisplayInfo::parents))
        _convert 0 [] sRootDisplayInfo
        ret |> Seq.toList

    let convertToConversationSourceFullVisibility =
        convertToConversationSource OpacityDecider.AlwaysVisible StatusVisibilityDecider.AlwaysVisible BackgroundColorDecider.DefaultColor

    let convertToConversationSourceFullVisibilityWithColor colorDecider =
        convertToConversationSource OpacityDecider.AlwaysVisible StatusVisibilityDecider.AlwaysVisible colorDecider

    let createConversationAt updatable addTo (details:StackPanel) conversationRows =
        let mainControls = WpfUtils.createConversationControls updatable addTo details
        let subControls = WpfUtils.updateConversation mainControls conversationRows
        mainControls, subControls

    let createConversation (details:StackPanel) conversationRows =
        createConversationAt false WpfUtils.End details conversationRows

module H = CommonConversationHelpers

module FilterAwareConversation = 
    
    let private getConversationControlOpacity (visibilityDecider:H.ConversationStatusVisibilityDecider) sDisplayInfo =
        if sDisplayInfo.StatusInfo.Status.LogicalStatusId < visibilityDecider.firstLogicalStatusId then
            OpacityOld
        else if sDisplayInfo.FilterInfo.Filtered then
            OpacityFiltered
        else OpacityVisible

    let fill (details:StackPanel) (filter:UIFilterDescriptor) statuses =    
        ldbg "UI: fillDetails"
        let visibilityDecider = new H.ConversationStatusVisibilityDecider(filter.ShowHidden)

        details.Children.Clear()
        statuses 
          |> Seq.toList
          |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
          |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
          |> List.map (fst >> (convertToStatusDisplayInfo filter))
          |> List.filter visibilityDecider.isRootStatusVisible
          |> List.map (H.convertToConversationSource { H.OpacityDecider.F          = getConversationControlOpacity visibilityDecider }
                                                     { H.StatusVisibilityDecider.F = visibilityDecider.isStatusVisible}
                                                     H.BackgroundColorDecider.DefaultColor)
          |> List.map (fun conversationRows -> H.createConversation details conversationRows)

module FullConversation = 

    let fill (details:StackPanel) statuses =    
        let noFilter = UIFilterDescriptor.NoFilter

        details.Children.Clear()
        statuses 
          |> Seq.toList
          |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
          |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
          |> List.map (fst >> (convertToStatusDisplayInfo noFilter))
          |> List.map H.convertToConversationSourceFullVisibility
          |> List.map (fun conversationRows -> H.createConversation details conversationRows)

    let addOne addTo (details:StackPanel) rootStatus =
        rootStatus 
          |> convertToStatusDisplayInfo UIFilterDescriptor.NoFilter
          |> H.convertToConversationSourceFullVisibility
          |> H.createConversationAt true addTo details

    let updateOne (conversationCtls:conversationControls) rootStatus =
        rootStatus 
          |> convertToStatusDisplayInfo UIFilterDescriptor.NoFilter
          |> H.convertToConversationSourceFullVisibility
          |> WpfUtils.updateConversation conversationCtls

    let updateOneWithColors lastUpdateAll (conversationCtls:conversationControls) rootStatus =
        let rec hasAnyNewParent = function
            | [] -> false
            | p::rest when p.StatusInfo.Status.Inserted >= lastUpdateAll -> true
            | p::rest -> hasAnyNewParent rest
        let colorF parents status =
            if status.StatusInfo.Status.Inserted >= lastUpdateAll || hasAnyNewParent parents then Brushes.Yellow
            else Brushes.White
        let colorDecider = { H.BackgroundColorDecider.F = colorF }
        rootStatus 
          |> convertToStatusDisplayInfo UIFilterDescriptor.NoFilter
          |> H.convertToConversationSourceFullVisibilityWithColor colorDecider
          |> WpfUtils.updateConversation conversationCtls