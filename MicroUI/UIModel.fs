module UIModel

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media
open Status
open Utils
open WpfUtils

/// Description of filtering info.
type UIFilterDescriptor = 
    { ShowHidden : bool 
      FilterOutRule : statusInfo -> bool } 
    with static member NoFilter = { ShowHidden = true; FilterOutRule = fun _ -> false }
type UISettingsDescriptor = 
    { Filter : UIFilterDescriptor
      ShowOnlyLinkPart : bool }
    with static member Default = { ShowOnlyLinkPart = true; Filter = UIFilterDescriptor.NoFilter }

/// Functions for statuses displayed as a tree - common functions.
module internal CommonConversationHelpers = 
    /// Helper type used in situation when conversations should be used and it should be decided whether
    /// the given node (in conversation) should be shown or not (depends also on children)
    type ConversationStatusVisibilityDecider(showHiddenStatuses) =
        // status id of first status (for retweets it is status id of the retweet, not the original status)
        let _firstLogicalStatusId = 
            match PreviewsState.userStatusesState.GetFirstStatusId() with
            | Some(value) -> value
            | None -> 0L

//        /// causes the filter that the status should not be displayed? (takes into account children as well)
//        let statusIsNotShownDueToFilter (sDisplayInfo:WpfUtils.StatusInfoToDisplay) = 
//            // dont' show hidden & is filtered & doesn't have unfiltered children
//            not showHiddenStatuses && 
//            sDisplayInfo.FilterInfo.Filtered &&
//            not sDisplayInfo.FilterInfo.HasUnfilteredDescendant

        member x.isStatusVisible (sInfo:statusInfo) (filterInfo: FilterInfo) =
            let isOlderThanFirstRequestedStatus = sInfo.Status.LogicalStatusId < _firstLogicalStatusId

            if isOlderThanFirstRequestedStatus then
                if 
                    filterInfo.HasUnfilteredDescendant || // if is older then first or is filtered, show only if there are unfiltered children
                    filterInfo.HasSomeDescendantsToShow   // or the descendants should be displayed (forced by showHiddenStatuses variable)
                then
                    VisibleByChildButIsTooOld
                else
                    Hidden
            else
                if not filterInfo.Filtered then
                    Visible
                else if filterInfo.HasUnfilteredDescendant then
                    VisibleByChildOtherwiseHidden
                else if showHiddenStatuses then
                    VisibleForced
                else 
                    Hidden

        //member x.firstLogicalStatusId = _firstLogicalStatusId

let rec internal convertToStatusDisplayInfo  (visibilityDecider:CommonConversationHelpers.ConversationStatusVisibilityDecider) filter (statusInfo:statusInfo)  : StatusInfoToDisplay =

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
        |> Seq.map (fun c -> convertToStatusDisplayInfo visibilityDecider filter c) 
        |> Seq.toList
    let filterInfo = convertToFilterInfo children
    {
        StatusInfo = statusInfo
        Children = children
        FilterInfo = filterInfo
        TextFragments = TextSplitter.splitText statusInfo.Status.Text
        Visibility = visibilityDecider.isStatusVisible statusInfo filterInfo
    }

let private resolveFragmentsFromCache sDisplayInfo = 
    { sDisplayInfo with TextFragments = sDisplayInfo.TextFragments |> WpfUtils.resolveTextFragmentsFromCache }

module LitlePreview = 
    //let GetModel (settings:UISettingsDescriptor) statuses : (PreviewFace * StatusInfoToDisplay) list =
    let GetModel (settings:UISettingsDescriptor) statuses : StatusInfoToDisplay list =
        let visibilityDecider = new CommonConversationHelpers.ConversationStatusVisibilityDecider(settings.Filter.ShowHidden)

        // todo: async
        statuses 
        |> Seq.toList
        |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
        |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
        |> List.map (fst >> (convertToStatusDisplayInfo visibilityDecider settings.Filter))

module FilterAwareConversation = 
    let GetModel (settings:UISettingsDescriptor) statuses : StatusInfoToDisplay list =
        let visibilityDecider = new CommonConversationHelpers.ConversationStatusVisibilityDecider(settings.Filter.ShowHidden)

         // todo: async
        statuses 
        |> Seq.toList
        |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
        |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
        |> List.map (fst >> (convertToStatusDisplayInfo visibilityDecider settings.Filter >> resolveFragmentsFromCache))
        |> List.filter (fun sInfo -> sInfo.Visibility <> Hidden)

/// Functions for statuses displayed as a tree. Functions are specific for use when conversations updates are performed - 
/// then different colors are used depending on status state (new, newly found ,..).
module FullConversation = 

    let GetModel statuses =    
        let noFilter = UIFilterDescriptor.NoFilter
        let visibilityDecider = new CommonConversationHelpers.ConversationStatusVisibilityDecider(true)

        statuses 
          |> Seq.toList
          |> List.map (fun sInfo -> (sInfo, StatusFunctions.GetNewestDisplayDateFromConversation sInfo))
          |> List.sortBy (fun (sInfo, displayDate) -> displayDate)
          |> List.map (fst >> (convertToStatusDisplayInfo visibilityDecider noFilter))

    let GetOneStatusModel status =
        let visibilityDecider = new CommonConversationHelpers.ConversationStatusVisibilityDecider(true)
        status |> convertToStatusDisplayInfo visibilityDecider UIFilterDescriptor.NoFilter