module WpfUtils

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Markup
open System.Windows.Documents
open System.Windows.Threading
open System.Windows.Media
open System.Diagnostics
open Status
open Utils
open TextSplitter

type FilterInfo = {
    Filtered : bool
    HasUnfilteredDescendant : bool
    HasSomeDescendantsToShow : bool
}
and StatusInfoToDisplay =
    {   StatusInfo : statusInfo
        Children : StatusInfoToDisplay list
        FilterInfo : FilterInfo
        mutable TextFragments : TextFragment []
    }
    member x.ExpandUrls() =
        ()

type PreviewFace = { 
    ImageOpacity : float
}
type PreviewSource = PreviewFace * StatusInfoToDisplay

type ConversationFace = { 
    Depth : int
    Opacity : float
}
type ConversationSource = ConversationFace * StatusInfoToDisplay

let (fontSize, pictureSize) = 
    let s = match Settings.Size with
            | "big" -> (14., 48.)
            | "medium" -> (13., 40.)
            | _ -> (12., 30.)
    linfop "UI size is {0}" s
    s

let private createPicture imagePath size margin = 
    let source = new Imaging.BitmapImage()
    source.BeginInit();
    source.UriSource <- new Uri(imagePath, System.UriKind.Relative);
    source.DecodePixelWidth <- int size
    source.EndInit()
    new Image(Source = source,
              Name = "image",
              Margin = margin,
              Width = size,
              Height = size,
              VerticalAlignment = VerticalAlignment.Top)
              //Stretch <- Media.Stretch.Uniform
let private createStatusPicture size margin (status:status) = 
    let pic = createPicture (ImagesSource.getImagePath status) pictureSize margin
    pic.ToolTip <- new ToolTip(Content = (sprintf "%s %A\n%s" status.UserName (status.Date.ToLocalTime()) status.Text))
    pic
let private getRetweetImage () =
    createPicture "retweet.png" 14. (new Thickness(1.5, 5., 0., 0.))    

let private BrowseHlClick (e:Navigation.RequestNavigateEventArgs) =
    Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)) |> ignore
    e.Handled <- true

let private linkFromUrl fragment =
    let link, text = TextSplitter.urlFragmentToLinkAndName fragment
    let hl = new Hyperlink(new Run(text),
                           NavigateUri = new Uri(link))
    hl.RequestNavigate.Add(BrowseHlClick)
    hl
let private textFragmentsToTextblock fragments = 
    let ret = new TextBlock(TextWrapping = TextWrapping.Wrap,
                            Padding = new Thickness(0.),
                            Margin = new Thickness(5., 0., 0., 5.),
                            FontSize = fontSize)
    for f in fragments do
        match f with
        | FragmentWords(w) -> ret.Inlines.Add(new Run(w))
        | _                -> let hl = linkFromUrl f
                              ret.Inlines.Add(hl)     
    ret

let createLittlePicture (previewFace, sDisplayInfo) = 
    let status = sDisplayInfo.StatusInfo.Status
    ldbgp "UI: Little picture for {0}" status
    let ret = createStatusPicture pictureSize (new Thickness(2.)) status
    ret.Opacity <- previewFace.ImageOpacity
    ldbgp "UI: Little picture for {0} done" status
    ret
              
let createDetail sDisplayInfo =
    let status = sDisplayInfo.StatusInfo.Status
    let row = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                             VerticalAlignment = VerticalAlignment.Top,
                             Orientation = Orientation.Horizontal,
                             Margin = new Thickness(0., 0., 0., 5.))
    let meta =
        let textInformation = 
            let m = new TextBlock(TextWrapping = TextWrapping.Wrap,
                                  Padding = new Thickness(0.),
                                  Margin = new Thickness(5., 0., 0., 5.))
            let hl userName statusId = 
                  let l = new Hyperlink(new Run(sprintf "%d" (statusId)),
                                        NavigateUri = new Uri(sprintf "http://twitter.com/#!/%s/status/%d" userName statusId))
                  l.RequestNavigate.Add(BrowseHlClick)
                  l
            let userName = 
                  if status.RetweetInfo.IsSome then
                      sprintf "%s (by @%s)" status.UserName status.RetweetInfo.Value.UserName
                  else
                      status.UserName
            [new Run(userName)                  :> Inline
             new Run(" | ")                     :> Inline
             hl status.UserName status.StatusId :> Inline
             new Run(" | ")           :> Inline
             new Run(sprintf "%s" (status.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"))) :> Inline
            ] |> List.iter m.Inlines.Add
            m
        let wrapTextInfoAndRetweetIcon () =
            let row = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                             VerticalAlignment = VerticalAlignment.Top,
                             Orientation = Orientation.Horizontal,
                             Margin = new Thickness(0.5, 0., 0., 0.))
            row.Children.Add(getRetweetImage()) |> ignore
            row.Children.Add(textInformation) |> ignore
            row
        match status.RetweetInfo with
        | None -> textInformation :> UIElement
        | Some(info) -> wrapTextInfoAndRetweetIcon () :> UIElement

    let imgContent = createStatusPicture pictureSize (new Thickness(5., 0., 0., 5.)) status
    let img = new Border(BorderBrush = Brushes.LightGray,
                     BorderThickness = new Thickness(0., 0., 0., 0.),
                     CornerRadius = new CornerRadius(2.),
                     Child = imgContent)

    let textInfo =
        let s = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                               VerticalAlignment = VerticalAlignment.Top,
                               Orientation = Orientation.Vertical,
                               Width = 500.,
                               Margin = new Thickness(0.))
        s.Children.Add(meta) |> ignore
        s.Children.Add((textFragmentsToTextblock sDisplayInfo.TextFragments)) |> ignore
        new Border(BorderBrush = Brushes.LightGray,
                   BorderThickness = new Thickness(0., 0., 0., 1.),
                   Child = s)
    
    row.Children.Add(img) |> ignore
    row.Children.Add(textInfo) |> ignore
    (row, img)

type conversationControls = {
    Wrapper : StackPanel
    Statuses : StackPanel
    UpdateButton : Button
    //DeleteButton : Button
}
type conversationNodeControlsInfo = 
    { Detail : StackPanel
      Img : Border
      StatusToDisplay : StatusInfoToDisplay 
    }
    member x.GetLogicalStatusId() =
        x.StatusToDisplay.StatusInfo.Status.LogicalStatusId
type detailTagContent = {
    mutable UrlResolved : bool
}

type ConversationControlPlacement =
  | End
  | Beginning

let createConversationControls (addTo:ConversationControlPlacement) (panel:StackPanel) =
    let conversationPnl = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                                         VerticalAlignment = VerticalAlignment.Top)
    let statusesPnl = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                                     VerticalAlignment = VerticalAlignment.Top)
    //conversationPnl.Background <- Brushes.Gray

    (*let delete = new Button(Content = "Delete", 
                            Width = 60.,
                            HorizontalAlignment = HorizontalAlignment.Left)
    conversationPnl.Children.Add(delete) |> ignore
    *)
    conversationPnl.Children.Add(statusesPnl) |> ignore

    match addTo with
    | End       -> panel.Children.Add(conversationPnl) |> ignore
    | Beginning -> panel.Children.Insert(0, conversationPnl) |> ignore

    {   Wrapper = conversationPnl
        Statuses = statusesPnl
        UpdateButton = null }
        //DeleteButton = delete  }
let addUpdateButton (controls:conversationControls) =
    let update = new Button(Content = "Update", 
                            Width = 60.,
                            HorizontalAlignment = HorizontalAlignment.Left)
                         //CommandParameter = (sourceStatus.StatusId :> obj)
    controls.Wrapper.Children.Add(update) |> ignore
    { controls with UpdateButton = update }
    
let updateConversation (controls:conversationControls) (updatedStatuses:ConversationSource list) =
//    controls.Statuses.Children.Clear()
//
//    let conversationCtl = new ResizeArray<_>()
//
//    let rec addTweets depth (currentStatus:StatusInfoToDisplay) =
//        let filterInfo = currentStatus.FilterInfo
//        let detail, img = createDetail currentStatus
//
//        img.Margin <- new Thickness(depth * (pictureSize+2.), 0., 0., 5.)
//        detail.Tag <- { UrlResolved = false }
//
//        controls.Statuses.Children.Add(detail) |> ignore
//
//        currentStatus.Children 
//            |> Seq.filter isStatusVisible
//            |> Seq.map (fun sInfo -> (sInfo, sInfo.StatusInfo.StatusId()))
//            |> Seq.sortBy (fun (_,id) -> id) 
//            |> Seq.iter (fun s -> addTweets (depth+1.) (fst s))
//        conversationCtl.Add({ Detail = detail
//                              Img = img
//                              StatusToDisplay = currentStatus})
//    // top level status should be visible, no need to test it; let's do it on descendants inside addTweets
//    addTweets 0. updatedStatus
//    conversationCtl |> Seq.toList
    let createConversationNode (conversationSource, sDisplayInfo) =
        let filterInfo = sDisplayInfo.FilterInfo
        let detail, img = createDetail sDisplayInfo

        img.Margin <- new Thickness(float conversationSource.Depth * (pictureSize+2.), 0., 0., 5.)
        detail.Tag <- { UrlResolved = false }
        detail.Opacity <- conversationSource.Opacity

        controls.Statuses.Children.Add(detail) |> ignore
        // todo: pouzit isStatusVisible
        { Detail = detail
          Img = img
          StatusToDisplay = sDisplayInfo}

    controls.Statuses.Children.Clear()
    updatedStatuses |> List.map createConversationNode
    
let createXamlWindow (file : string) = 
  use xmlReader = System.Xml.XmlReader.Create(file)
  System.Windows.Markup.XamlReader.Load(xmlReader) :?> Window
  
let dispatchMessage<'a> (dispatcherOwner:DispatcherObject) fce = 
    dispatcherOwner.Dispatcher.Invoke(DispatcherPriority.Normal, 
                                      System.Action<'a>(fce), 
                                      ()) |> ignore