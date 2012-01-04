module UrlShortenerFunctions

open System.Text.RegularExpressions
open Utils
open System.Net
open System.IO

//todo: http://trunc.it/, http://jdem.cz

type ExtractedUrlsType = 
    | Extracted of string
    | NotNeeded of string
    | Failed

let private textFromResponse (response:WebResponse option) =
    match response with
    | None    -> 
        //printfn "No response, no text"
        None
    | Some(r) -> 
        use stream = r.GetResponseStream()
        use reader = new StreamReader(stream)
        let t = reader.ReadToEnd()
        //printfn "Parsed text %s" t
        Some(t)

let private extractFromMeta = function
    | None -> None
    | Some(t) ->
        let regex = "<META\\s*http-equiv=\"refresh\"\s+content=\"[^\"]+URL=(?<url>[^\"]+)\""
        match Regex.Match(t, regex) with
        | m when m.Success -> 
            //printfn "Parsed meta"
            //File.AppendAllLines(@"log.log", [t])
            Some(m.Groups.["url"].Value)
        | _ -> 
            //printfn "Unable to parse meta from %s" t
            None

let private getResponse (url:string) =
    //System.Diagnostics.Debugger.Break()
    try
        let request = System.Net.WebRequest.Create url :?> System.Net.HttpWebRequest
        request.AllowAutoRedirect <- false
        request.Timeout <- 1000*30;
        request.UserAgent <- "Mozilla/5.0 (Windows NT 6.0) AppleWebKit/534.30 (KHTML, like Gecko) Chrome/12.0.742.112 Safari/534.30"
        request.Accept <- "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8"
        request.Headers.["Accept-Encoding"] <- "deflate"
        request.Headers.["Accept-Language"] <- "cs-CZ,cs;q=0.8"
        //printfn "Getting response for %s" url
        Some(request.GetResponse())
    with ex -> 
        printfn "Error: %A" ex
        None

let private tco (url:string) =
    let toExpand = url.TrimEnd([|')'; ';'; ':'|])
    let parsedUrl = toExpand |> getResponse
                             |> textFromResponse
                             |> extractFromMeta
    //printfn "Parsed from tco: %A" parsedUrl
    //File.AppendAllLines(@"log.log", ["from tco"; parsedUrl.ToString()])
    match parsedUrl with
    |Some(u) -> Extracted(u)
    |None    -> Failed

let private generalExtract (url:string) =
    match getResponse(url) with
    | None -> Failed
    | Some(response) -> 
        match response.Headers.["Location"] with
        | null 
        | ""  -> Failed
        | str -> 
            //File.AppendAllLines(@"D:\backup\github-src\MicroTools\test\log.log", ["from g"; str], System.Text.Encoding.UTF8); 
            Extracted(str)

let extract (url:string) =
    let regex = "^https?://(bit.ly|bitly.com|is.gd|j.mp|cli.gs|tinyurl.com|snurl.com|goo.gl|tr.im|t.co|jdem.cz|trunc.it)/"
    let rec _extract (url:string) =
        let extracted = 
            if url.StartsWith("http://t.co") then
                tco url
            else if Regex.IsMatch(url, regex) then
                generalExtract url
            else
                NotNeeded(url)
        match extracted with
        | Failed -> Failed              // finish
        | Extracted(u) -> _extract u    // try to extract again, it might be e.g. bit.ly wrapped in t.co
        | NotNeeded(u) -> Extracted(u)  // finish

    if Regex.IsMatch(url, regex) then
        let ret = _extract url
        linfop2 "Expanded {0} to {1}" url ret
        ret
    else
        NotNeeded(url)