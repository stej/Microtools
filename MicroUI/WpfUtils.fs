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

type StatusInfoToDisplay = {
    StatusInfo : statusInfo
    Filtered : bool
    Children : StatusInfoToDisplay list
    HasUnfilteredDescendant : bool
    HasSomeDescendantsToShow : bool
}
/// Adds information about filtering
///  @filterDoesntHideStatuses - user requested to show hidden statuses
///  @filterer - returns true if the status is filtered out (matches the filter)
let rec convertToFilterInfo filterDoesntHideStatuses (filterer: statusInfo->bool) (statusInfo:statusInfo) =
    let children = 
        statusInfo.Children 
        |> Seq.map (fun c -> convertToFilterInfo filterDoesntHideStatuses filterer c) 
        |> Seq.toList
    let existsUnfilteredDescendant = 
        children |> List.exists (fun c -> not c.Filtered || c.HasUnfilteredDescendant)
    let hasSomeVisibleDescendant =
        existsUnfilteredDescendant || (filterDoesntHideStatuses && statusInfo.Children.Count > 0)

    { StatusInfo = statusInfo
      Filtered = filterer statusInfo
      Children = children
      HasUnfilteredDescendant = existsUnfilteredDescendant 
      HasSomeDescendantsToShow = hasSomeVisibleDescendant }
      
let regexUrl = new System.Text.RegularExpressions.Regex("(?<user>@\w+)|" + 
                                                        "(?<hash>#\w+)|" +
                                                        "(?<url>https?:(?://|\\\\)+(?:[\w\-]+\.)+[\w]+(?:/?$|[\w\d:#@%/;$()~_?+\-=\\\.&*]*[\w\d:#@%/;$()~_+\-=\\&*]))")

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

let private linkFromText text =
    let matchGroups = regexUrl.Match(text).Groups
    let url, txt = if matchGroups.["url"].Success then text,text
                   else if matchGroups.["user"].Success then (sprintf "http://twitter.com/%s" (text.TrimStart('@')), text)
                   else if matchGroups.["hash"].Success then (sprintf "http://twitter.com/search?q=%s" (System.Web.HttpUtility.UrlEncode(text)), text)
                   else failwith "unknown regex group"
    let hl = new Hyperlink(new Run(txt),
                           NavigateUri = new Uri(url))
    hl.RequestNavigate.Add(BrowseHlClick)
    hl
let private textToTextblock (text:string) = 
    ldbgp "Parsing {0}" text
    let parts = regexUrl.Split(System.Web.HttpUtility.HtmlDecode(text))

    let ret = new TextBlock(TextWrapping = TextWrapping.Wrap,
                            Padding = new Thickness(0.),
                            Margin = new Thickness(5., 0., 0., 5.),
                            FontSize = fontSize)
    for part in parts do
        if regexUrl.IsMatch(part) then
            ldbgp "Parsed url: {0}" part
            try
                let hl = linkFromText part
                ret.Inlines.Add(hl)
            with ex ->
                lerrex ex (sprintf "Url %s parsed incorrectly" part)
                ret.Inlines.Add(new Run(part))
        else
            ret.Inlines.Add(new Run(part))
    ret

let createLittlePicture status = 
    createStatusPicture 30. (new Thickness(2.)) status 
              
let createDetail (status:status) =
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
        s.Children.Add((textToTextblock status.Text)) |> ignore
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
type conversationNodeControlsInfo = {
    Detail : StackPanel
    Img : Border
    // rest is copied from StatusInfoToDisplay - otherwise if I add field
    // StatusInfo : StatusInfoToDisplay - there would be too many dots in other code like detailCtl.StatusInfo.StatusInfo.Status.LogicalStatusId
    StatusInfo : statusInfo
    Filtered : bool
    Children : StatusInfoToDisplay list
    HasUnfilteredDescendant : bool
    HasSomeDescendantsToShow : bool
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
    
let updateConversation (controls:conversationControls) (isStatusVisible:StatusInfoToDisplay->bool) (updatedStatus:StatusInfoToDisplay) =
    controls.Statuses.Children.Clear()

    let conversationCtl = new ResizeArray<_>()

    let rec addTweets depth (currentStatus:StatusInfoToDisplay) =
        let detail, img = createDetail currentStatus.StatusInfo.Status
        img.Margin <- new Thickness(depth * (pictureSize+2.), 0., 0., 5.)
        controls.Statuses.Children.Add(detail) |> ignore
        currentStatus.Children 
            |> Seq.filter isStatusVisible
            |> Seq.map (fun sInfo -> (sInfo, sInfo.StatusInfo.StatusId()))
            |> Seq.sortBy (fun (_,id) -> id) 
            |> Seq.iter (fun s -> addTweets (depth+1.) (fst s))
        conversationCtl.Add({ Detail = detail
                              Img = img
                              StatusInfo = currentStatus.StatusInfo
                              Filtered = currentStatus.Filtered
                              Children = currentStatus.Children
                              HasUnfilteredDescendant = currentStatus.HasUnfilteredDescendant
                              HasSomeDescendantsToShow = currentStatus.HasSomeDescendantsToShow})
    // top level status should be visible, no need to test it; let's do it on descendants inside addTweets
    addTweets 0. updatedStatus
    conversationCtl |> Seq.toList

/// partial, set just the function; remove
let setNewConversation = updateConversation
    
let createXamlWindow (file : string) = 
  use xmlReader = System.Xml.XmlReader.Create(file)
  System.Windows.Markup.XamlReader.Load(xmlReader) :?> Window
  
let dispatchMessage<'a> (dispatcherOwner:DispatcherObject) fce = 
    dispatcherOwner.Dispatcher.Invoke(DispatcherPriority.Normal, 
                                      System.Action<'a>(fce), 
                                      ()) |> ignore