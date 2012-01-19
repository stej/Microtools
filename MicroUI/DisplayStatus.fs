module DisplayStatus

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media
open Status
open Utils
open WpfUtils

let OpacityFiltered, OpacityOld, OpacityVisible = 0.2, 0.5, 1.0
let StatusDefaultBrush, NewStatusBrush, NewlyFoundStatusBrush = Brushes.Transparent, Brushes.Yellow, Brushes.LightSalmon

/// Description of filtering info.
type UIFilterDescriptor = 
    { ShowHidden : bool 
      FilterOutRule : statusInfo -> bool } 
    with static member NoFilter = { ShowHidden = true; FilterOutRule = fun _ -> false }
type UISettingsDescriptor = 
    { Filter : UIFilterDescriptor
      ShowOnlyLinkPart : bool }
    with static member Default = { ShowOnlyLinkPart = true; Filter = UIFilterDescriptor.NoFilter }

/// Used in TwitterConversation. Provides info needed to pick one color for status background.
type UIColorsDescriptor = 
    { NewInConversation : statusInfo seq
      LastUpdate : System.DateTime
      AlwaysDefault : bool }
    with 
        static member UseDefault = 
            { NewInConversation = []; LastUpdate = DateTime.Now; AlwaysDefault = true }
        static member ByLastUpdate date = 
            { NewInConversation = []; LastUpdate = date; AlwaysDefault = false }
        static member StatusAsNew = 
            { NewInConversation = []; LastUpdate = DateTime.MinValue; AlwaysDefault = false }
        static member ByNewStatusesAndLastUpdate parents date = 
            { NewInConversation = parents; LastUpdate = date; AlwaysDefault = false }

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

let private resolveFragmentsFromCache sDisplayInfo = 
    { sDisplayInfo with TextFragments = sDisplayInfo.TextFragments |> WpfUtils.resolveTextFragmentsFromCache }

let private convertToLittleSDisplayInfo = convertToStatusDisplayInfo
let private convertToFullSDisplayInfo filter = convertToStatusDisplayInfo filter >> resolveFragmentsFromCache

/// Functions for preview consisting only from images.
module LitlePreview = 
    let private convertToPreviewSource sDisplayInfo =
        ({ ImageOpacity = if sDisplayInfo.FilterInfo.Filtered then OpacityFiltered else OpacityVisible },
         sDisplayInfo)

    // todo: run only needed parts (manipulating with controls) on UI thread.. 
    // currently all the stuff is supposed to run on UI thread
    let fill (wrap:WrapPanel) (settings:UISettingsDescriptor) statuses =

        let previewSources = 
            statuses 
            |> Seq.toList
            |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
            |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
            |> List.map (fst 
                         >> (convertToLittleSDisplayInfo settings.Filter)
                         >> convertToPreviewSource)

        WpfUtils.dispatchMessage wrap (fun _ -> 
            wrap.Children.Clear()
            previewSources 
                |> List.map WpfUtils.createLittlePicture
                |> List.iter (wrap.Children.Add >> ignore)
        )

        previewSources |> List.map snd

/// Functions for statuses displayed as a tree - common functions.
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
        with static member AlwaysVisible = { F = fun _ -> true }
    type BackgroundColorDecider =
        { F : StatusInfoToDisplay list -> StatusInfoToDisplay -> SolidColorBrush }
        with 
            static member DefaultColor = { F = fun _ _ -> StatusDefaultBrush }
            static member FullByDescriptor descriptor = 
                { F = fun parents status -> 
                        if descriptor.AlwaysDefault then
                            StatusDefaultBrush
                        else if BackgroundColorDecider.statusInNews descriptor.NewInConversation status.StatusInfo then
                            NewlyFoundStatusBrush
                        else if status.StatusInfo.Status.Inserted >= descriptor.LastUpdate ||
                            BackgroundColorDecider.hasAnyNewParent descriptor.LastUpdate parents then
                            NewStatusBrush
                        else
                            StatusDefaultBrush
                }
            static member private hasAnyNewParent timeFrom parents =
                let rec call =  function
                    | [] -> false
                    | p::rest when p.StatusInfo.Status.Inserted >= timeFrom -> true
                    | p::rest -> call rest
                call parents
            static member private statusInNews news sInfo = // returns true if passed status is news list
                news |> Seq.exists (fun childInfo -> childInfo.Status.StatusId = sInfo.Status.StatusId)                            

    let convertToConversationSource (opacityDecider:OpacityDecider) 
                                    (visibilityDecider:StatusVisibilityDecider) 
                                    (colorDecider: BackgroundColorDecider) 
                                    (showOnlyDomainLinks : bool)
                                    sRootDisplayInfo =
        let ret = new ResizeArray<_>()
        let rec _convert depth parents sDisplayInfo = 
            ret.Add({ Depth = depth
                      Opacity = opacityDecider.F sDisplayInfo
                      BackgroundColor = colorDecider.F parents sDisplayInfo
                      ShowOnlyDomainInLinks = showOnlyDomainLinks}, 
                    sDisplayInfo)
            sDisplayInfo.Children
                |> Seq.filter visibilityDecider.F
                |> Seq.map (fun sInfo -> (sInfo, sInfo.StatusInfo.StatusId()))
                |> Seq.sortBy snd
                |> Seq.map fst
                |> Seq.iter (_convert (depth+1) (sDisplayInfo::parents))
        _convert 0 [] sRootDisplayInfo
        ret |> Seq.toList

    let convertToConversationSourceWithDefaults =
        convertToConversationSource OpacityDecider.AlwaysVisible StatusVisibilityDecider.AlwaysVisible BackgroundColorDecider.DefaultColor false

    let convertToConversationSourceWithDefaultsWithColor colorDecider =
        convertToConversationSource OpacityDecider.AlwaysVisible StatusVisibilityDecider.AlwaysVisible colorDecider false

    let createConversationAt updatable addTo (details:StackPanel) conversationRows =
        let mainControls = WpfUtils.createConversationControls updatable addTo details
        let subControls = WpfUtils.updateConversation mainControls conversationRows
        mainControls, subControls

    let createConversation (details:StackPanel) conversationRows =
        createConversationAt false WpfUtils.End details conversationRows

module H = CommonConversationHelpers

/// Functions for statuses displayed as a tree. Functions are specific for use when filter should be considered.
module FilterAwareConversation = 
    
    let private getConversationControlOpacity (visibilityDecider:H.ConversationStatusVisibilityDecider) sDisplayInfo =
        if sDisplayInfo.StatusInfo.Status.LogicalStatusId < visibilityDecider.firstLogicalStatusId then
            OpacityOld
        else if sDisplayInfo.FilterInfo.Filtered then
            OpacityFiltered
        else OpacityVisible

    let fill (details:StackPanel) (settings:UISettingsDescriptor) statuses =    
        ldbg "UI: fillDetails"
        let visibilityDecider = new H.ConversationStatusVisibilityDecider(settings.Filter.ShowHidden)

        let conversationsSource =
            statuses 
            |> Seq.toList
            |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
            |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
            |> List.map (fst >> (convertToFullSDisplayInfo settings.Filter))
            |> List.filter visibilityDecider.isRootStatusVisible
            |> List.map (H.convertToConversationSource { H.OpacityDecider.F          = getConversationControlOpacity visibilityDecider }
                                                        { H.StatusVisibilityDecider.F = visibilityDecider.isStatusVisible}
                                                        H.BackgroundColorDecider.DefaultColor
                                                        settings.ShowOnlyLinkPart)

        let ctls = ref []
        WpfUtils.dispatchMessage details (fun _ -> 
            details.Children.Clear()
            ctls := conversationsSource |> List.map (fun conversationRows -> H.createConversation details conversationRows)
        )
        !ctls

    let updateText uiSettings (ctlInfo:WpfUtils.conversationNodeControlsInfo) =
        WpfUtils.updateTextblockText uiSettings.ShowOnlyLinkPart ctlInfo.Text ctlInfo.StatusToDisplay.TextFragments

/// Functions for statuses displayed as a tree. Functions are specific for use when conversations updates are performed - 
/// then different colors are used depending on status state (new, newly found ,..).
module FullConversation = 

    let fill (details:StackPanel) statuses =    
        let noFilter = UIFilterDescriptor.NoFilter

        details.Children.Clear()
        statuses 
          |> Seq.toList
          |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
          |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
          |> List.map (fst >> (convertToFullSDisplayInfo noFilter))
          |> List.map H.convertToConversationSourceWithDefaults
          |> List.map (fun conversationRows -> H.createConversation details conversationRows)

    let addOneWithColor colorsDescriptor addTo (details:StackPanel) rootStatus =
        let colorDecider = H.BackgroundColorDecider.FullByDescriptor colorsDescriptor
        rootStatus 
          |> convertToFullSDisplayInfo UIFilterDescriptor.NoFilter
          |> H.convertToConversationSourceWithDefaultsWithColor colorDecider
          |> H.createConversationAt true addTo details

    let addOne addTo (details:StackPanel) rootStatus =
        addOneWithColor UIColorsDescriptor.UseDefault addTo details rootStatus

    let updateOne (conversationCtls:conversationControls) rootStatus =
        rootStatus 
          |> convertToFullSDisplayInfo UIFilterDescriptor.NoFilter
          |> H.convertToConversationSourceWithDefaults
          |> WpfUtils.updateConversation conversationCtls

    let updateOneWithColors (colorsDescriptor:UIColorsDescriptor) (conversationCtls:conversationControls) rootStatus =
        
        let colorDecider = H.BackgroundColorDecider.FullByDescriptor colorsDescriptor
        rootStatus 
          |> convertToFullSDisplayInfo UIFilterDescriptor.NoFilter
          |> H.convertToConversationSourceWithDefaultsWithColor colorDecider
          |> WpfUtils.updateConversation conversationCtls