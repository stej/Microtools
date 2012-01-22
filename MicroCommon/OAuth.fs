module OAuth

open System
open System.IO
open DevDefined.OAuth
open Utils

let private consumerKey = "8Zb9ZmCKaLAJ3dqsArStA" 
let private consumerSecret = "PfqnoKucLZZO8jX8ghVIHeLsjOvs5uZhrmdsTtZAOss"

let mutable private accessTokenPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "twitter.accesstoken.txt")
let mutable private accessToken:Framework.IToken = null

let private createToken consumerKey realm token tokenSecret =
    new Framework.TokenBase(ConsumerKey = consumerKey,
                            Realm = realm,
                            Token = token,
                            TokenSecret = tokenSecret)

let private saveToken (path:string) token =
    linfo "saving token.."
    use file = new StreamWriter(path)
    file.WriteLine(accessToken.ConsumerKey)
    file.WriteLine(accessToken.Realm)
    file.WriteLine(accessToken.Token)
    file.Write(accessToken.TokenSecret)
    file.Close()
    linfo "..done"

let readAccessToken () =
    linfo "reading token.."
    accessToken <- 
        if File.Exists(accessTokenPath) then
            match File.ReadAllLines(accessTokenPath) with
            | [|ck; realm; token; tokensecret|] -> createToken ck realm token tokensecret
            | _ -> failwith (sprintf "File %s doesn't contain access token information." accessTokenPath)
        else
            printf "%s doesn't exist, so the request will not be handled.\nEither call the function again or register on Twitter" accessTokenPath
            null

//let private getAccessToken() =
//    createToken accessToken.ConsumerKey accessToken.Realm accessToken.Token accessToken.TokenSecret

let private getNewSession() =
    let cons = new Consumer.OAuthConsumerContext(
                        ConsumerKey = consumerKey,
                        ConsumerSecret = consumerSecret,
                        SignatureMethod = Framework.SignatureMethod.HmacSha1)

    new Consumer.OAuthSession (
                        cons,
                        "http://twitter.com/oauth/request_token",
                        "http://twitter.com/oauth/authorize",
                        "http://twitter.com/oauth/access_token")

let requestTwitterWithQS url (queryStringParams: (string*string) list) =
    if accessToken = null then
        failwith "token is not initialized"

    try
        let req = getNewSession().Request(accessToken) // getAccessToken())
        req.Context.RequestMethod <- "GET"
        req.Context.RawUri <- new System.Uri(url)
        req.Timeout <- Nullable<int>(1000 * 30) // 30 sec

        // add query string params
        queryStringParams |> List.iter req.Context.QueryParameters.Add
        try 
            let response = req.ToWebResponse()
            Some(DevDefined.OAuth.Utility.StreamExtensions.ReadToEnd(response), response.StatusCode, response.Headers)
        with
          | :? System.Net.WebException as ex -> lerrex ex "Unable to download string"
                                                let response = ex.Response :?> System.Net.HttpWebResponse
                                                match response with 
                                                | null -> None
                                                | instance -> Some("", instance.StatusCode, instance.Headers)
    with 
        ex -> lerrex ex "Error when querying url"
              None

let requestTwitter url =
    requestTwitterWithQS url []

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
        printfn "First authorize the application so that it can access your Twitter account. Look for register.bat file"
        exit 1

let setAccessTokenPath path =
    accessTokenPath <- path

readAccessToken ()