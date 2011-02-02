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

let createLittlePicture (status:status) = 
    let source = new Imaging.BitmapImage()
    let imagePath = ImagesSource.getImagePath status
    source.BeginInit();
    source.UriSource <- new Uri(imagePath, System.UriKind.Relative);
    source.DecodePixelWidth <- 30
    source.EndInit()
    new Image(Source = source,
              Name = "image",
              Margin = new Thickness(2.),
              Width = 30.,
              Height = 30.,
              ToolTip = new ToolTip(Content = (sprintf "%s %A\n%s" status.UserName status.Date status.Text)),
              VerticalAlignment = VerticalAlignment.Top)
    //Background = Brushes.Gray
              
let createDetail (status:status) =
    let row = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                             VerticalAlignment = VerticalAlignment.Top,
                             Orientation = Orientation.Horizontal)
    let textInfo = new StackPanel(HorizontalAlignment = HorizontalAlignment.Left,
                                  VerticalAlignment = VerticalAlignment.Top,
                                  Orientation = Orientation.Vertical,
                                  Width = 500.)
    let img = 
        let i = createLittlePicture status
        //i.Stretch <- Media.Stretch.Uniform
        i.Margin <- new Thickness(5., 0., 0., 5.)
        i
    let nameAndId = 
        let hl = new Hyperlink(new Run(sprintf "%s - %d - %s" status.UserName status.StatusId (status.Date.ToString("yyyy-MM-dd HH:mm:ss"))),
                                       NavigateUri = new Uri(sprintf "http://twitter.com/#!/%s/status/%d" status.UserName status.StatusId))
        hl.RequestNavigate.Add(fun e -> Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)) |> ignore; e.Handled <- true )
        new Label(Content = hl,
                  Padding = new Thickness(0.),
                  Margin = new Thickness(5., 0., 0., 2.))
    let text = new TextBlock(Text = status.Text, 
                             TextWrapping = TextWrapping.Wrap,
                             Padding = new Thickness(0.),
                             Margin = new Thickness(5., 0., 0., 5.))
    textInfo.Children.Add(nameAndId) |> ignore
    textInfo.Children.Add(text) |> ignore
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
        img.Margin <- new Thickness(depth * 32., 0., 0., 5.)
        if (showAsNew currentStatus) then detail.Background <- Brushes.Yellow
        controls.Statuses.Children.Add(detail) |> ignore
        currentStatus.Children |> Seq.iter (fun s -> addTweets (depth+1.) s showAsNew)
    addTweets 0. status showAsNew

/// partial, set just the function
let setNewConversation = updateConversation (fun _ -> false)
    
//let createAppState() =
//   let sp = new StackPanel(Orientation = Orientation.Horizontal)
//    sp.Children.Add(new Label(Content = "Twitter limit: ")
//    sp.Children.Add(

let createXamlWindow (file : string) = 
  use xmlReader = XmlReader.Create(file)
  System.Windows.Markup.XamlReader.Load(xmlReader) :?> Window
  
//let dispatchMessage<'a> (dispatcherOwner:DispatcherObject) fce arguments = 
//    dispatcherOwner.Dispatcher.Invoke(DispatcherPriority.Normal, 
//                                      System.Action<'a>(fce), 
//                                      arguments) |> ignore
let dispatchMessage<'a> (dispatcherOwner:DispatcherObject) fce = 
    dispatcherOwner.Dispatcher.Invoke(DispatcherPriority.Normal, 
                                      System.Action<'a>(fce), 
                                      ()) |> ignore