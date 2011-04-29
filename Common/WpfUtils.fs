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
open System.IO
open Status

open System.Windows.Media.Imaging

let regexUrl = new System.Text.RegularExpressions.Regex("(?<user>@\w+)|" + "(?<url>https?:(?://|\\\\)+(?:[\w\-]+\.)+[\w]+(?:/?$|[\w\d:#@%/;$()~_?+\-=\\\.&*]*[\w\d:#@%/;$()~_+\-=\\&*]))")

let (fontSize, pictureSize) = 
    let s = match System.Configuration.ConfigurationManager.AppSettings.["size"].ToString() with
            | "big" -> (14., 48.)
            | "medium" -> (13., 40.)
            | _ -> (12., 30.)
    printf "UI size is %A" s
    s

let private createPicture size margin (status:status) =
    (* 
    use fs = File.Open(@"d:\temp\TwitterConversation\bin\Debug\images\Twitter-JustinEtheredge-Photo_on_2011-02-21_at_17.11__3_normal.jpg", System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite)
    let decoder = BitmapDecoder.Create(fs, BitmapCreateOptions.None, BitmapCacheOption.Default);
    // grab the bitmap frame, which contains the metadata
    let frame = decoder.Frames.[0];
    // get the metadata as BitmapMetadata
    let metadata = frame.Metadata; // :> BitmapMetadata
    // close the stream before returning
    fs.Close()
 
    // return a null array if keywords don't exist.  otherwise, return a string array
    //if metadata != null && metadata.Keywords != null then
        //let a =  metadata.Keywords.ToArray()
        //printfn "%A" a
        *)


    let source = new Imaging.BitmapImage(CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreColorProfile)
    let imagePath = ImagesSource.getImagePath status
    source.BeginInit();
    source.UriSource <- new Uri(imagePath, System.UriKind.RelativeOrAbsolute);
    source.DecodePixelWidth <- int pictureSize
    source.CacheOption <- BitmapCacheOption.OnLoad
    source.EndInit()
    new Image(Source = source,
              Name = "image",
              Margin = margin,
              Width = pictureSize,
              Height = pictureSize,
              ToolTip = new ToolTip(Content = (sprintf "%s %A\n%s" status.UserName status.Date status.Text)),
              VerticalAlignment = VerticalAlignment.Top)
              //Stretch <- Media.Stretch.Uniform
          
    (*let source = new Imaging.BitmapImage() //CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreColorProfile
    let imagePath = ImagesSource.getImagePath status
    source.BeginInit();
    source.UriSource <- new Uri(imagePath, System.UriKind.RelativeOrAbsolute);
    source.StreamSource <- System.IO.File.OpenRead(imagePath)
    source.DecodePixelWidth <- int pictureSize
    source.EndInit()
    let imageData = Array.create (int source.StreamSource.Length) 0uy
    source.StreamSource.Seek(0L, System.IO.SeekOrigin.Begin) |> ignore
    source.StreamSource.Read(imageData, 0, imageData.Length) |> ignore

    new Image(Source = source,
              Name = "image",
              Margin = margin,
              Width = pictureSize,
              Height = pictureSize,
              ToolTip = new ToolTip(Content = (sprintf "%s %A\n%s" status.UserName status.Date status.Text)),
              VerticalAlignment = VerticalAlignment.Top)
              //Stretch <- Media.Stretch.Uniform*)

let private BrowseHlClick (e:Navigation.RequestNavigateEventArgs) =
    Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)) |> ignore
    e.Handled <- true

let private linkFromText text =
    let matchGroups = regexUrl.Match(text).Groups
    let url, txt = if matchGroups.["url"].Success then text,text
                   else if matchGroups.["user"].Success then (sprintf "http://twitter.com/%s" (text.TrimStart('@')), text)
                   else failwith "unknown regex group"
    let hl = new Hyperlink(new Run(txt),
                           NavigateUri = new Uri(url))
    hl.RequestNavigate.Add(BrowseHlClick)
    hl
let private textToTextblock (text:string) = 
    Utils.log Utils.Debug (sprintf "Parsing %s" text)
    let parts = regexUrl.Split(System.Web.HttpUtility.HtmlDecode(text))

    let ret = new TextBlock(TextWrapping = TextWrapping.Wrap,
                            Padding = new Thickness(0.),
                            Margin = new Thickness(5., 0., 0., 5.),
                            FontSize = fontSize)
    for part in parts do
        if regexUrl.IsMatch(part) then
            Utils.log Utils.Debug (sprintf "Parsed url: %s" part)
            try
                let hl = linkFromText part
                ret.Inlines.Add(hl)
            with ex ->
                Utils.log Utils.Error (sprintf "Wrong parsed url: %s" part)
                printfn "Url %s parsed incorrectly" part
                ret.Inlines.Add(new Run(part))
        else
            ret.Inlines.Add(new Run(part))
    ret

let createLittlePicture status = 
    createPicture 30. (new Thickness(2.)) status 
              
let createDetail (status:status) =
    let row = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                             VerticalAlignment = VerticalAlignment.Top,
                             Orientation = Orientation.Horizontal,
                             Margin = new Thickness(0., 0., 0., 5.))
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
type conversationNodeControlsInfo = {
    Detail : StackPanel
    Img : Border
    Status: status
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
    
let updateConversation (controls:conversationControls) status =
    controls.Statuses.Children.Clear()

    let conversationCtl = new ResizeArray<_>()
    let rec addTweets depth currentStatus =
        let detail, img = createDetail currentStatus
        img.Margin <- new Thickness(depth * (pictureSize+2.), 0., 0., 5.)
        controls.Statuses.Children.Add(detail) |> ignore
        currentStatus.Children 
            |> Seq.sortBy (fun s -> s.StatusId) 
            |> Seq.iter (fun s -> addTweets (depth+1.) s)
        conversationCtl.Add({ Detail = detail
                              Img = img
                              Status = currentStatus})
    addTweets 0. status
    conversationCtl |> Seq.toList

/// partial, set just the function; remove
let setNewConversation = updateConversation
    
let createXamlWindow (file : string) = 
  use xmlReader = XmlReader.Create(file)
  System.Windows.Markup.XamlReader.Load(xmlReader) :?> Window
  
let dispatchMessage<'a> (dispatcherOwner:DispatcherObject) fce = 
    dispatcherOwner.Dispatcher.Invoke(DispatcherPriority.Normal, 
                                      System.Action<'a>(fce), 
                                      ()) |> ignore