module WpfUtils

open System
open System.Xml
open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Markup
open System.Windows.Documents
open System.Windows.Threading
open System.Windows.Media
open System.Diagnostics
open Status

let private regexUrl = new System.Text.RegularExpressions.Regex("(https?:(?://|\\\\)+[\w\d:#@%/;$()~_?+\-=\\\.&*]*[\w\d:#@%/;$()~_+\-=\\&*])")

let (fontSize, pictureSize) = 
    match System.Configuration.ConfigurationManager.AppSettings.["size"].ToString() with
    | "big" -> (14., 48.)
    | _ -> (12., 30.)

let private createPicture size margin (status:status) = 
    let source = new Imaging.BitmapImage()
    let imagePath = ImagesSource.getImagePath status
    source.BeginInit();
    source.UriSource <- new Uri(imagePath, System.UriKind.Relative);
    source.DecodePixelWidth <- int pictureSize
    source.EndInit()
    new Image(Source = source,
              Name = "image",
              Margin = margin,
              Width = pictureSize,
              Height = pictureSize,
              ToolTip = new ToolTip(Content = (sprintf "%s %A\n%s" status.UserName status.Date status.Text)),
              VerticalAlignment = VerticalAlignment.Top)
              //Stretch <- Media.Stretch.Uniform

let private BrowseHlClick (e:Navigation.RequestNavigateEventArgs) =
    Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)) |> ignore
    e.Handled <- true

let private textToTextblock (text:string) = 
    let parts = regexUrl.Split(System.Web.HttpUtility.HtmlDecode(text))

    let ret = new TextBlock(TextWrapping = TextWrapping.Wrap,
                            Padding = new Thickness(0.),
                            Margin = new Thickness(5., 0., 0., 5.),
                            FontSize = fontSize)
    parts 
    |> Seq.iter (fun part ->
        if regexUrl.IsMatch(part) then
            let hl = new Hyperlink(new Run(part),
                                   NavigateUri = new Uri(part))
            hl.RequestNavigate.Add(BrowseHlClick)
            ret.Inlines.Add(hl)
        else
            ret.Inlines.Add(new Run(part))
    )
    ret

let createLittlePicture status = 
    createPicture 30. (new Thickness(2.)) status 
              
let createDetail (status:status) =
    let row = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                             VerticalAlignment = VerticalAlignment.Top,
                             Orientation = Orientation.Horizontal)
    let meta =
        let m = new TextBlock(TextWrapping = TextWrapping.Wrap,
                              Padding = new Thickness(0.),
                              Margin = new Thickness(5., 0., 0., 5.))
        let hl = 
          let l = new Hyperlink(new Run(sprintf "%d" (status.StatusId)),
                                NavigateUri = new Uri(sprintf "http://twitter.com/#!/%s/status/%d" status.UserName status.StatusId))
          l.RequestNavigate.Add(BrowseHlClick)
          l
        [new Run(status.UserName) :> Inline
         new Run(" | ")           :> Inline
         hl                       :> Inline
         new Run(" | ")           :> Inline
         new Run(sprintf "%s" (status.Date.ToString("yyyy-MM-dd HH:mm:ss"))) :> Inline]
        |> List.iter m.Inlines.Add
        m

    let imgContent = createPicture pictureSize (new Thickness(5., 0., 0., 5.)) status
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

type ConversationControlPlacement =
  | End
  | Beginning

let createConversationControls addUpdate (addTo:ConversationControlPlacement) (panel:StackPanel) =
    let conversationPnl = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                                         VerticalAlignment = VerticalAlignment.Top)
    let statusesPnl = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                                     VerticalAlignment = VerticalAlignment.Top)
    //conversationPnl.Background <- Brushes.Gray

    let update = if addUpdate then
                    new Button(Content = "Update", 
                               Width = 60.,
                               HorizontalAlignment = HorizontalAlignment.Left)
                            //CommandParameter = (sourceStatus.StatusId :> obj)
                 else
                    null

    (*let delete = new Button(Content = "Delete", 
                            Width = 60.,
                            HorizontalAlignment = HorizontalAlignment.Left)*)

    //conversationPnl.Children.Add(delete) |> ignore
    if addUpdate then
       conversationPnl.Children.Add(update) |> ignore
    conversationPnl.Children.Add(statusesPnl) |> ignore

    match addTo with
    | End       -> panel.Children.Add(conversationPnl) |> ignore
    | Beginning -> panel.Children.Insert(0, conversationPnl) |> ignore

    {   Wrapper = conversationPnl
        Statuses = statusesPnl
        UpdateButton = update }
        //DeleteButton = delete  }

let updateConversation (showAsNew:status -> bool) (controls:conversationControls) status =
    controls.Statuses.Children.Clear()

    let rec addTweets depth currentStatus (showAsNew:status -> bool) =
        let detail, img = createDetail currentStatus
        img.Margin <- new Thickness(depth * (pictureSize+2.), 0., 0., 5.)
        if (showAsNew currentStatus) then detail.Background <- Brushes.Yellow
        controls.Statuses.Children.Add(detail) |> ignore
        currentStatus.Children |> Seq.iter (fun s -> addTweets (depth+1.) s showAsNew)
    addTweets 0. status showAsNew

/// partial, set just the function
let setNewConversation = updateConversation (fun _ -> false)
    
let createXamlWindow (file : string) = 
  use xmlReader = XmlReader.Create(file)
  System.Windows.Markup.XamlReader.Load(xmlReader) :?> Window
  
let dispatchMessage<'a> (dispatcherOwner:DispatcherObject) fce = 
    dispatcherOwner.Dispatcher.Invoke(DispatcherPriority.Normal, 
                                      System.Action<'a>(fce), 
                                      ()) |> ignore