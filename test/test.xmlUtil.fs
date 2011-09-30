module test.xmlUtil

open NUnit.Framework
open FsUnit
open System.Xml
open Utils

[<TestFixture>] 
type ``Given some xml document`` ()=
    let xml = new XmlDocument()
    do
      xml.LoadXml("<x><a>value A</a><b>value B<bInside>value inside</bInside></b></x>")

    [<Test>] member test.
      ``xpathValue for bInside node should return 'value inside'`` () =
            xml |> xpathValue "//bInside" |> should equal "value inside"