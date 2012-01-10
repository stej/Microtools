module Utils

open System
open System.Threading
open System.Xml

log4net.Config.XmlConfigurator.Configure()
let private logger =  log4net.LogManager.GetLogger("loggerdefault");
type LogLevel =
    | Debug
    | Info
    | Error
let private log level str =
    //ILog logger = LogManager.GetLogger("notes");
    if level = Debug && logger.IsDebugEnabled then 
        logger.Debug(str)
    if level = Info  && logger.IsInfoEnabled  then 
        logger.Info(str)
        printfn "%s" str
    if level = Error && logger.IsErrorEnabled then 
        logger.Error(str)
        printfn "%s" str
let private logex anexception str =
    logger.Error(str, anexception) 

let ldbg str = log Debug str
let ldbgp format (p1:obj) = log Debug (String.Format(format, [p1]))
let ldbgp2 format (p1:obj) (p2:obj) = log Debug (String.Format(format, p1, p2))
let linfo str = log Info str
let linfop format (p1:obj) = log Info (String.Format(format, [p1]))
let linfop2 format (p1:obj) (p2:obj) = log Info (String.Format(format, p1, p2))
let lerr str = log Error str
let lerrp format (p1:obj) = log Error (String.Format(format, [p1]))
let lerrp2 format (p1:obj) (p2:obj) = log Error (String.Format(format, p1, p2))
let lerrex exc str = logex exc str

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
    try 
        xml.SelectSingleNode(path).InnerText
    with e ->
        lerrex e (sprintf "Unable to parse %s" path)
        ""

let xpathNodes path (xml:XmlNode) =
    xml.SelectNodes(path)

let Int64Default = -1L
let IntDefault = -1

let Int64OrDefault (value:string) =
    match Int64.TryParse(value) with
        | (true, i) -> i
        | _ -> Int64Default
let IntOrDefault (value:string) =
    match Int32.TryParse(value) with
        | (true, i) -> i
        | _ -> IntDefault
let monthsMap = 
  Map ([("Jan", 1); ("Feb", 2); ("Mar", 3); ("Apr", 4); ("May", 5); ("Jun", 6);
        ("Jul", 7); ("Aug", 8); ("Sep", 9); ("Oct", 10); ("Nov", 11); ("Dec", 12)])
let TwitterDateOrDefault (value:string) =
    //printfn "Parsing date %s" value
    let r = new System.Text.RegularExpressions.Regex("^(?<dayInWeek>\w+)\s(?<month>\w+)\s(?<day>\d+)\s(?<h>\d+):(?<m>\d+):(?<s>\d+)\s\+(\d+)\s(?<year>\d+)$")
    let mtch  = r.Match(value)
    if not mtch.Success then 
      lerrp "Value {0} is not valid date" value
      System.DateTime.MinValue
    else 
      let groups = mtch.Groups
      new System.DateTime(
        groups.["year"].Value |> IntOrDefault,
        monthsMap.[groups.["month"].Value],
        groups.["day"].Value |> IntOrDefault,
        groups.["h"].Value |> IntOrDefault,
        groups.["m"].Value |> IntOrDefault,
        groups.["s"].Value |> IntOrDefault,
        DateTimeKind.Utc)
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

let private urlShortenerRegex = new System.Text.RegularExpressions.Regex @"^(https?://([^./]+\.)+\w+)/?(?<rest>.*)"
let shortenUrlToDomain (url:string) = 
    let m = urlShortenerRegex.Match(url)
    if m.Success then
        let short = urlShortenerRegex.Replace(url, "$1")
        match m.Groups.["rest"].Success && not (String.IsNullOrEmpty(m.Groups.["rest"].Value)) with
        | true -> short + "/..."
        | false -> short
    else
        url
    
    
(*let padSpaces count = 
    System.Console.Write("{0," + (count * 3).ToString() + "}", "")
*)

type Settings =
    static member private settings = System.Configuration.ConfigurationManager.AppSettings;
    static member MinRateLimit = match Settings.settings.["minRateLimit"] with | null -> 20 | filter -> int filter
    static member Size = Settings.settings.["size"].ToString()
    static member UpCount = match Settings.settings.["upCount"] with | null -> 50 | c -> int c
    static member TwitterClientFetchInterval = match Settings.settings.["statusesDownloadInterval"] with | null -> 60*3 | c -> int c

let doAndRet fce item = 
    fce item |> ignore
    item

let isValidRegex regex =
    try
        new System.Text.RegularExpressions.Regex(regex) |> ignore
        true
    with _ ->
        false
        
(*
// by Tomas Petricek
let synchronize f = 
  let ctx = System.Threading.SynchronizationContext.Current 
  f (fun g arg ->
    let nctx = System.Threading.SynchronizationContext.Current 
    if ctx <> null && ctx <> nctx then ctx.Post((fun _ -> g(arg)), null)
    else g(arg) )
type Microsoft.FSharp.Control.Async with 
  static member AwaitObservable(ev1:IObservable<'a>) =
    synchronize (fun f ->
      Async.FromContinuations((fun (cont,econt,ccont) -> 
        let rec callback = (fun value ->
          remover.Dispose()
          f cont value )
        and remover : IDisposable  = ev1.Subscribe(callback) 
        () )))
  static member AwaitObservable(ev1:IObservable<'a>, ev2:IObservable<'b>) = 
    synchronize (fun f ->
      Async.FromContinuations((fun (cont,econt,ccont) -> 
        let rec callback1 = (fun value ->
          remover1.Dispose()
          remover2.Dispose()
          f cont (Choice1Of2(value)) )
        and callback2 = (fun value ->
          remover1.Dispose()
          remover2.Dispose()
          f cont (Choice2Of2(value)) )
        and remover1 : IDisposable  = ev1.Subscribe(callback1) 
        and remover2 : IDisposable  = ev2.Subscribe(callback2) 
        () )))
*)