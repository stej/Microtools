module test.statusParsing

open NUnit.Framework
open FsUnit
open System.Xml
open System.IO
open Utils
open Status
open StatusXmlProcessors

type ``Given status xml document`` ()=
    let xml = new XmlDocument()
    let xmlWithPhoto = new XmlDocument()
    let xmlWithTwoPhotos = new XmlDocument()
    do
      xml.Load(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "testStatus.xml"))
      xmlWithPhoto.Load(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "testPhotoStatus.xml"))
      xmlWithTwoPhotos.Load(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "testMorePhotoStatus.xml"))

    [<Test>] 
    member test.``parse status`` () =
        let s = xml2Status (xml.SelectSingleNode("status"))
        //printfn "status %A" s
        s |> should not (equal None)
        s.Value.StatusId |> should equal 164474168182714368L
        s.Value.Text     |> should equal "Co je ACTA? (titulky CZ/SK/EN): http://t.co/wnobaUTk via @youtube"
        s.Value.UserName |> should equal "jirkavagner"
        s.Value.Date     |> should equal (System.DateTime.Parse("2012-01-31 22:24:32"))
        s.Value.UserFavoritesCount |> should equal 1
        s.Value.UserStatusesCount |> should equal 623

    [<Test>]
    member test.``parse url entity`` () =
        let node = xml.SelectSingleNode("status")
        let sInfo = { Status = (xml2Status node).Value
                      Children = new ResizeArray<_>()
                      Source = Timeline }
        let urls = ExtraProcessors.Url.extractEntities sInfo node |> Seq.toList
        urls.Length |> should equal 1
        urls.[0].LongUrl  |> should equal "http://youtu.be/3cz6bEkdX4Q"
        urls.[0].ShortUrl |> should equal "http://t.co/wnobaUTk"
        urls.[0].StatusId |> should equal (sInfo.StatusId())

    [<Test>]
    member test.``parse photo entity`` () =
        let node = xmlWithPhoto.SelectSingleNode("status")
        let sInfo = { Status = (xml2Status node).Value
                      Children = new ResizeArray<_>()
                      Source = Timeline }
        let photos = ExtraProcessors.Photo.extractEntities sInfo node |> Seq.toList
        photos.Length |> should equal 1
        photos.[0].Id |> should equal "171695616248905728"
        photos.[0].ShortUrl |> should equal "http://t.co/MnyeLFwj"
        photos.[0].LongUrl |> should equal "http://twitter.com/stejcz/status/171695616240517121/photo/1"
        photos.[0].ImageUrl |> should equal "http://p.twimg.com/AmH8QNgCAAABtv7.jpg"
        photos.[0].StatusId |> should equal (sInfo.StatusId())
        photos.[0].Sizes    |> should equal "thumb,medium,large,small"

    [<Test>]
    member test.``parse more photo entities`` () =
        let node = xmlWithTwoPhotos.SelectSingleNode("status")
        let sInfo = { Status = (xml2Status node).Value
                      Children = new ResizeArray<_>()
                      Source = Timeline }
        let photos = ExtraProcessors.Photo.extractEntities sInfo node |> Seq.toList
        photos.Length |> should equal 2
        photos.[0].Id |> should equal "171695616248905728"
        photos.[0].ShortUrl |> should equal "http://t.co/MnyeLFwj"
        photos.[0].LongUrl |> should equal "http://twitter.com/stejcz/status/171695616240517121/photo/1"
        photos.[0].ImageUrl |> should equal "http://p.twimg.com/AmH8QNgCAAABtv7.jpg"
        photos.[0].StatusId |> should equal (sInfo.StatusId())
        photos.[0].Sizes    |> should equal "thumb,medium,large,small"
        photos.[1].Id |> should equal "123"
        photos.[1].ShortUrl |> should equal "short"
        photos.[1].LongUrl |> should equal "expanded"
        photos.[1].ImageUrl |> should equal "mu"
        photos.[1].StatusId |> should equal (sInfo.StatusId())
        photos.[1].Sizes    |> should equal "thumb"
        
[<TestFixture>] 
type ``Given retweet xml document`` ()=
    let xml = new XmlDocument()
    do
      let path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "testRetweet.xml")
      printfn "%s" path
      xml.Load(path)

    [<Test>] 
    member test.``parse retweet`` () =
        let s = xml2Retweet (xml.SelectSingleNode("status"))
        //printfn "status %A" s
        s |> should not (equal None)
        s.Value.StatusId |> should equal 165203029522513920L
        s.Value.Text     |> should equal "Windows Phone 8 Apollo: Windows 8 kernel, more form factors. The Windows 8 family is getting complicated. http://t.co/R0e3s9QV"
        s.Value.UserName |> should equal "timanderson"
        s.Value.Date     |> should equal (System.DateTime.Parse("2012-02-02 22:40:46"))
        s.Value.UserFavoritesCount |> should equal 0
        s.Value.UserStatusesCount |> should equal 7774
        
        s.Value.RetweetInfo |> should not (equal None)
        let retweet = s.Value.RetweetInfo.Value
        retweet.UserName           |> should equal "slavof"
        retweet.UserFavoritesCount |> should equal 1
        retweet.UserStatusesCount  |> should equal 1408

    [<Test>]
    member test.``parse url entity`` () =
        let node = xml.SelectSingleNode("status")
        let sInfo = { Status = (xml2Retweet node).Value
                      Children = new ResizeArray<_>()
                      Source = Timeline }
        let urls = ExtraProcessors.Url.extractEntities sInfo node |> Seq.toList
        urls.Length |> should equal 1
        urls.[0].LongUrl  |> should equal "http://bit.ly/yVaHSH"
        urls.[0].ShortUrl |> should equal "http://t.co/R0e3s9QV"
        urls.[0].StatusId |> should equal (sInfo.StatusId())

[<TestFixture>] 
type ``Given some xml status that might be Retweet or STatus`` ()=
    let rxml = new XmlDocument()
    let sxml = new XmlDocument()
    do
      let rpath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "testRetweet.xml")
      let spath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "testStatus.xml")
      printfn "%s / %s" rpath spath
      rxml.Load(rpath)
      sxml.Load(spath)

    [<Test>] 
    member test.``try to use xml2StatusOrRetweet on Retweet`` () =
        let s = xml2StatusOrRetweet (rxml.SelectSingleNode("status"))
        s.Value.RetweetInfo |> should not (equal None)
        //s.Value.RetweetInfo |> should equal None

    [<Test>] 
    member test.``try to use xml2StatusOrRetweet on Status`` () =
        let s = xml2StatusOrRetweet (sxml.SelectSingleNode("status"))
        s.Value.RetweetInfo |> should equal None
        //s.Value.RetweetInfo |> should not (equal None)