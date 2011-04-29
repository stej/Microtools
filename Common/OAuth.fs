module OAuth

open System
open System.IO
open DevDefined.OAuth

let consumerKey = "8Zb9ZmCKaLAJ3dqsArStA" 
let consumerSecret = "PfqnoKucLZZO8jX8ghVIHeLsjOvs5uZhrmdsTtZAOss"

let accessTokenPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "twitter.accesstoken.txt")
let mutable (accessToken:Framework.IToken) = null

let createToken consumerKey realm token tokenSecret =
    new Framework.TokenBase(ConsumerKey = consumerKey,
                            Realm = realm,
                            Token = token,
                            TokenSecret = tokenSecret)

let readToken path =
    printf "reading.."
    if File.Exists(path) then
        match File.ReadAllLines(path) with
        | [|ck; realm; token; tokensecret|] -> createToken ck realm token tokensecret
        | _ -> failwith (sprintf "File %s doesn't contain access token information." path)
    else
        failwith (sprintf "File %s doesn't exist." path)

let saveToken (path:string) token =
    printf "saving.."
    use file = new StreamWriter(path)
    file.WriteLine(accessToken.ConsumerKey)
    file.WriteLine(accessToken.Realm)
    file.WriteLine(accessToken.Token)
    file.Write(accessToken.TokenSecret)
    file.Close()

accessToken <- 
    if File.Exists(accessTokenPath) then
        readToken accessTokenPath
    else
        //failwith (sprintf "%s doesn't exist, so the request will not be handled.\nEither call the function again or register on Twitter" accessTokenPath)
        null

let getAccessToken() =
    createToken accessToken.ConsumerKey accessToken.Realm accessToken.Token accessToken.TokenSecret

let getNewSession() =
    let cons = new Consumer.OAuthConsumerContext(
                        ConsumerKey = consumerKey,
                        ConsumerSecret = consumerSecret,
                        SignatureMethod = Framework.SignatureMethod.HmacSha1)

    new Consumer.OAuthSession (
                        cons,
                        "http://twitter.com/oauth/request_token",
                        "http://twitter.com/oauth/authorize",
                        "http://twitter.com/oauth/access_token")
let requestTwitter url =
    if accessToken = null then
        failwith "token is not initialized"

    try
        let req = getNewSession().Request(getAccessToken())
        req.Context.RequestMethod <- "GET"
        req.Context.RawUri <- new System.Uri(url)
        let req0 = req.ToWebRequest()
        req0.Timeout <- 1000 * 30 // 30 sec
        try 
            //let response = req.ToWebResponse()
            let response = req0.GetResponse() :?> System.Net.HttpWebResponse    // by reflector
            Some(DevDefined.OAuth.Utility.StreamExtensions.ReadToEnd(response), response)
        with
          | :? System.Net.WebException as ex -> Some("", ex.Response :?> System.Net.HttpWebResponse)
    with 
        ex -> printfn "%s" ex.Message
              None

let registerOnTwitter() =
    let session = getNewSession()
    let rtoken = session.GetRequestToken()
    let authLink = session.GetUserAuthorizationUrlForToken(rtoken, "go");

    System.Diagnostics.Process.Start(authLink) |> ignore

    printf "Type PIN returned on Twitter page: "
    let pin =  System.Console.ReadLine()
    accessToken <- session.ExchangeRequestTokenForAccessToken(rtoken, pin)
    saveToken accessTokenPath accessToken

let checkAccessTokenFile() =
    if accessToken = null then
        registerOnTwitter()