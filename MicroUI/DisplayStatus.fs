module DisplayStatus

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media
open Status
open Utils
open WpfUtils
open UIModel

let OpacityFiltered, OpacityOld, OpacityVisible = 0.3, 0.5, 1.0
let StatusDefaultBrush, NewStatusBrush, NewlyFoundStatusBrush = Brushes.Transparent, Brushes.Yellow, Brushes.LightSalmon

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

/// Functions for preview consisting only from images.
module LitlePreview = 
    let private convertToPreviewSource sDisplayInfo =
        ({ ImageOpacity = if sDisplayInfo.FilterInfo.Filtered then OpacityFiltered else OpacityVisible },
         sDisplayInfo)

    // todo: run only needed parts (manipulating with controls) on UI thread.. 
    // currently all the stuff is supposed to run on UI thread
    let fill (wrap:WrapPanel) statusInfos2Display =

        let previewSource = 
            statusInfos2Display |> List.map convertToPreviewSource

        WpfUtils.dispatchMessage wrap (fun _ -> 
            wrap.Children.Clear()
            previewSource 
                |> List.map WpfUtils.createLittlePicture
                |> List.iter (wrap.Children.Add >> ignore)
        )

module CommonConversationHelpers = 

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
    
    let private getConversationControlOpacity sDisplayInfo =
        match sDisplayInfo.Visibility with
        | WpfUtils.Visible -> OpacityVisible
        | WpfUtils.VisibleByChildButIsTooOld -> OpacityOld
        | WpfUtils.VisibleByChildOtherwiseHidden -> OpacityFiltered
        | WpfUtils.Hidden -> lerrp "Status {0} is taken and opacity examined" sDisplayInfo.StatusInfo
                             OpacityVisible
        | WpfUtils.VisibleForced -> OpacityFiltered

    let fill (details:StackPanel) (settings:UISettingsDescriptor) statusInfos2Display =    
        ldbg "UI: fillDetails"

        let conversationsSource =
            statusInfos2Display
            |> List.map (H.convertToConversationSource { H.OpacityDecider.F          = getConversationControlOpacity }
                                                       { H.StatusVisibilityDecider.F = fun s -> s.Visibility <> WpfUtils.Hidden}
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

    let fill (details:StackPanel) statusInfos2Display =    
        details.Children.Clear()
        statusInfos2Display 
          |> List.map H.convertToConversationSourceWithDefaults
          |> List.map (fun conversationRows -> H.createConversation details conversationRows)

    let addOneWithColor colorsDescriptor addTo (details:StackPanel) rootStatus =
        let colorDecider = H.BackgroundColorDecider.FullByDescriptor colorsDescriptor
        rootStatus 
          |> H.convertToConversationSourceWithDefaultsWithColor colorDecider
          |> H.createConversationAt true addTo details

    let addOne addTo (details:StackPanel) rootStatus =
        addOneWithColor UIColorsDescriptor.UseDefault addTo details rootStatus

    let updateOne (conversationCtls:conversationControls) rootStatus =
        rootStatus 
          |> H.convertToConversationSourceWithDefaults
          |> WpfUtils.updateConversation conversationCtls

    let updateOneWithColors (colorsDescriptor:UIColorsDescriptor) (conversationCtls:conversationControls) rootStatus =
        
        let colorDecider = H.BackgroundColorDecider.FullByDescriptor colorsDescriptor
        rootStatus 
          |> H.convertToConversationSourceWithDefaultsWithColor colorDecider
          |> WpfUtils.updateConversation conversationCtls