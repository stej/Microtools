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
open ShortenerDbInterface

let mutable (urlResolver : UrlResolver.UrlResolver) = (Array.zeroCreate 1).[0] // omg trick from http://cs.hubfs.net/topic/None/57408
type FilterInfo = {
    Filtered : bool
    HasUnfilteredDescendant : bool
    HasSomeDescendantsToShow : bool
}
and StatusInfoToDisplay =
    {   StatusInfo : statusInfo
        Children : StatusInfoToDisplay list
        FilterInfo : FilterInfo
        mutable TextFragments : TextFragment array
    }
    member x.ExpandUrls() =
        let expandUrl url =
            urlResolver.AsyncResolveUrl(url, x.StatusInfo.StatusId())
        let expand () =
            async { 
                let expanded = x.TextFragments 
                               |> Array.map (fun f -> match f with |FragmentUrl(u) -> let newu = expandUrl u |> Async.RunSynchronously // eh, todo
                                                                                      FragmentUrl(newu)
                                                                   | x -> x) 
                return expanded
            }

        async { let! expanded = expand ()
                x.TextFragments <- expanded }

type PreviewFace = { 
    ImageOpacity : float
}
type PreviewSource = PreviewFace * StatusInfoToDisplay

type ConversationFace = { 
    Depth : int
    Opacity : float
    BackgroundColor: SolidColorBrush
    ShowOnlyDomainInLinks : bool
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

let private linkFromUrl (hyperlinkContentGetter: _ -> string*string*string) fragment =
    let link, text, tooltip = hyperlinkContentGetter fragment
    let hl = new Hyperlink(new Run(text),
                           NavigateUri = new Uri(link),
                           TextDecorations = null,
                           ToolTip = tooltip)
    hl.RequestNavigate.Add(BrowseHlClick)
    hl, link, text
let private generalLinkFromUrl showOnlyDomainInLinks fragment = 
    
    let hyperlinkContentGetter = fun fragment -> 
        let mutable link, text = TextSplitter.urlFragmentToLinkAndName fragment
        let tooltip = text
        if showOnlyDomainInLinks then
            text <- Utils.shortenUrlToDomain text
        link, text, tooltip
    let hl, link, text = linkFromUrl hyperlinkContentGetter fragment
    hl
let private twitterLinkFromUrl fragment = 
    let hyperlinkContentGetter = fun fragment -> 
        let link, text = TextSplitter.urlFragmentToLinkAndName fragment
        link, text, text
    let hl, link, text = linkFromUrl hyperlinkContentGetter fragment
    hl

type private TextBlockInnerCtl =    // needed to define here, because Run and Hyperlink don't have suitable common base class
    | CtlRun of Run
    | CtlHL of Hyperlink
let private textFragmentsToCtls showOnlyDomainInLinks fragments = 
    seq {
        for f in fragments do match f with | FragmentWords(w) -> yield CtlRun(new Run(w))
                                           | FragmentUrl(w)   -> yield CtlHL((generalLinkFromUrl showOnlyDomainInLinks f))
                                           | _                -> yield CtlHL((twitterLinkFromUrl f))
    }
let private fillTextBlock (tb:TextBlock) controls = 
    controls |> Seq.iter (fun c -> match c with
                                    | CtlRun(r) -> tb.Inlines.Add(r)
                                    | CtlHL(h)  -> tb.Inlines.Add(h))
let private textFragmentsToTextblock showOnlyDomainInLinks fragments = 
    let ret = new TextBlock(TextWrapping = TextWrapping.Wrap,
                            Padding = new Thickness(0.),
                            Margin = new Thickness(5., 0., 0., 5.),
                            FontSize = fontSize)
    fragments |> textFragmentsToCtls showOnlyDomainInLinks
              |> fillTextBlock ret
    ret

let createLittlePicture (previewFace, sDisplayInfo) = 
    let status = sDisplayInfo.StatusInfo.Status
    ldbgp "UI: Little picture for {0}" status
    let ret = createStatusPicture pictureSize (new Thickness(2.)) status
    ret.Opacity <- previewFace.ImageOpacity
    ldbgp "UI: Little picture for {0} done" status
    ret
              
let createDetail (conversationSource, sDisplayInfo) =
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

    let innerTextBlock, textInfo =
        let s = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                               VerticalAlignment = VerticalAlignment.Top,
                               Orientation = Orientation.Vertical,
                               Width = 500.,
                               Margin = new Thickness(0.))
        let textCtls = textFragmentsToTextblock conversationSource.ShowOnlyDomainInLinks sDisplayInfo.TextFragments
        s.Children.Add(meta) |> ignore
        s.Children.Add(textCtls) |> ignore
        (textCtls, 
         new Border(BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(0., 0., 0., 1.),
                    Child = s))
    
    row.Children.Add(img) |> ignore
    row.Children.Add(textInfo) |> ignore
    (row, img, innerTextBlock)

/// Used when content of the textblock changes and should be updated (currently only because urls are extracted)
let updateTextblockText showOnlyDomainInLinks (tb:TextBlock) fragments = 
    tb.Inlines.Clear()
    fragments |> textFragmentsToCtls showOnlyDomainInLinks 
              |> fillTextBlock tb

type conversationControls = {
    Wrapper : StackPanel
    Statuses : StackPanel
    UpdateButton : Button
    //DeleteButton : Button
}
type conversationNodeControlsInfo = 
    { Detail : StackPanel
      Img : Border
      Text : TextBlock
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

let createConversationControls updatable (addTo:ConversationControlPlacement) (panel:StackPanel) =
    let conversationPnl = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                                         VerticalAlignment = VerticalAlignment.Top)
    let statusesPnl = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                                     VerticalAlignment = VerticalAlignment.Top)

    (*let delete = new Button(Content = "Delete", 
                            Width = 60.,
                            HorizontalAlignment = HorizontalAlignment.Left)
    conversationPnl.Children.Add(delete) |> ignore
    *)
    conversationPnl.Children.Add(statusesPnl) |> ignore

    match addTo with
    | End       -> panel.Children.Add(conversationPnl) |> ignore
    | Beginning -> panel.Children.Insert(0, conversationPnl) |> ignore

    let update = 
        if updatable then
            let u = new Button(Content = "Update", 
                                    Width = 60.,
                                    HorizontalAlignment = HorizontalAlignment.Left)
            conversationPnl.Children.Add(u) |> ignore
            u
        else
            null

    {   Wrapper = conversationPnl
        Statuses = statusesPnl
        UpdateButton = update }
        //DeleteButton = delete  }
    
let updateConversation (controls:conversationControls) (updatedStatuses:ConversationSource list) =
    ldbgp "Updating conversation, list size: {0}" updatedStatuses.Length
    let createConversationNode (conversationSource, sDisplayInfo) =
        ldbgp "Adding {0}" sDisplayInfo.StatusInfo
        let filterInfo = sDisplayInfo.FilterInfo
        let detail, img, textblock = createDetail (conversationSource, sDisplayInfo)

        img.Margin <- new Thickness(float conversationSource.Depth * (pictureSize+2.), 0., 0., 5.)
        detail.Tag <- { UrlResolved = false }
        detail.Opacity <- conversationSource.Opacity
        detail.Background <- conversationSource.BackgroundColor

        controls.Statuses.Children.Add(detail) |> ignore
        { Detail = detail
          Img = img
          Text = textblock
          StatusToDisplay = sDisplayInfo }

    controls.Statuses.Children.Clear()
    updatedStatuses |> List.map createConversationNode
    
let createXamlWindow (file : string) = 
  use xmlReader = System.Xml.XmlReader.Create(file)
  System.Windows.Markup.XamlReader.Load(xmlReader) :?> Window
  
let dispatchMessage<'a> (dispatcherOwner:DispatcherObject) fce = 
    dispatcherOwner.Dispatcher.Invoke(DispatcherPriority.Normal, 
                                      System.Action<'a>(fce), 
                                      ()) |> ignore

let resolveTextFragmentsFromCache fragments =
    fragments |> Array.map (fun f -> 
         match f with |FragmentUrl(u) -> let newu = urlResolver.AsyncResolveUrlFromCache(u) |> Async.RunSynchronously // eh, todo
                                         match newu with
                                         | Some(longUrl) -> FragmentUrl(longUrl)
                                         | None -> FragmentUrl(u)
                      | x -> x)

module Commands =
    //http://dikhi.wordpress.com/2008/06/27/keygesture-doesnt-work-with-alpha-numeric-key/
    open System.Windows.Input
    type AnyKeyGesture(key) = 
        inherit InputGesture()
        let _key = key
        override this.Matches (targetElement, inputEventArgs) =
            match inputEventArgs with
            | :? KeyEventArgs as args -> _key = args.Key
            | _ -> false

    let bindCommand key fn (window:Window) (menuItem:MenuItem) =
        //http://stackoverflow.com/questions/1361350/keyboard-shortcuts-in-wpf
        let rt = new RoutedCommand()
        //let gesture = new AnyKeyGesture(key)      // fires when i type the key in textbox -- not desired -> added CTRL modifier
        let gesture = new KeyGesture(key, ModifierKeys.Control)
        rt.InputGestures.Add(gesture) |> ignore
        let commandBinding = new CommandBinding(rt, 
                                                new ExecutedRoutedEventHandler(fun _ _ -> fn()))
        window.CommandBindings.Add(commandBinding) |> ignore
        if menuItem <> null then
            menuItem.Command <- rt
            menuItem.InputBindings.Add(new InputBinding(rt, gesture)) |> ignore

    let bindClick mouseAction fn (window:Window) (menuItem:MenuItem) =
        let rt = new RoutedCommand()
        let gesture = new MouseGesture(mouseAction)
        rt.InputGestures.Add(gesture) |> ignore
        let commandBinding = new CommandBinding(rt, 
                                                new ExecutedRoutedEventHandler(fun _ _ -> fn()))
        window.CommandBindings.Add(commandBinding) |> ignore