module Utils

open System
open System.Threading
open System.Xml

let convertJsonToXml (json:string) = 
  match json with
    | null -> failwith "null not expected"
    | _ ->let bytes = System.Text.Encoding.UTF8.GetBytes(json)
          let quotas = System.Xml.XmlDictionaryReaderQuotas.Max
          let reader = System.Runtime.Serialization.Json.JsonReaderWriterFactory.CreateJsonReader(bytes, quotas)
          try
              let xml = new System.Xml.XmlDocument()
              xml.Load(reader)
              xml
          finally
            reader.Close()

let xpathValue path (xml:XmlNode) =
    xml.SelectSingleNode(path).InnerText

let xpathNodes path (xml:XmlNode) =
    xml.SelectNodes(path)

let Int64OrDefault (value:string) =
    match Int64.TryParse(value) with
        | (true, i) -> i
        | _ -> -1L
let IntOrDefault (value:string) =
    match Int32.TryParse(value) with
        | (true, i) -> i
        | _ -> -1
let monthsMap = 
  Map ([("Jan", 1); ("Feb", 2); ("Mar", 3); ("Apr", 4); ("May", 5); ("Jun", 6);
        ("Jul", 7); ("Aug", 8); ("Sep", 9); ("Oct", 10); ("Nov", 11); ("Dec", 12)])
let TwitterDateOrDefault (value:string) =
    //printfn "Parsing date %s" value
    let r = new System.Text.RegularExpressions.Regex("^(?<dayInWeek>\w+)\s(?<month>\w+)\s(?<day>\d+)\s(?<h>\d+):(?<m>\d+):(?<s>\d+)\s\+(\d+)\s(?<year>\d+)$")
    let mtch  = r.Match(value)
    if not mtch.Success then 
      printf "Value %s is not valid date" value
      System.DateTime.MinValue
    else 
      let groups = mtch.Groups
      new System.DateTime(
        groups.["year"].Value |> IntOrDefault,
        monthsMap.[groups.["month"].Value],
        groups.["day"].Value |> IntOrDefault,
        groups.["h"].Value |> IntOrDefault,
        groups.["m"].Value |> IntOrDefault,
        groups.["s"].Value |> IntOrDefault)
let BoolOrDefault deflt (value:string) =
    try Convert.ToBoolean(value)
    with ex -> deflt

let download (url:string) = 
    //printfn "downloading %s" url
    let client = new System.Net.WebClient()
    client.DownloadString url
    
let getSyncContext() = 
    let syncContext = SynchronizationContext.Current
    do if syncContext = null then failwith "no synchronization context found"
    syncContext
let triggerEvent fce (syncContext:SynchronizationContext) =
    syncContext.Post(SendOrPostCallback(fce), state=null)
    
let padSpaces count = 
    System.Console.Write("{0," + (count * 3).ToString() + "}", "")
    
log4net.Config.XmlConfigurator.Configure()
let private logger =  log4net.LogManager.GetLogger("loggerdefault");
type LogLevel =
    | Debug
    | Info
    | Error
let log level str =
    //ILog logger = LogManager.GetLogger("notes");
    if level = Debug && logger.IsDebugEnabled then 
        logger.Debug(str)
    if level = Info  && logger.IsInfoEnabled  then 
        logger.Info(str)
        printf "%s" str
    if level = Error && logger.IsErrorEnabled then 
        logger.Error(str)
        printf "%s" str