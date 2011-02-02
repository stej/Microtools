module ImagesSource

open System.IO

let private directory = "images"

let private getImagePathFromUrl url user =
    let imageName = Path.GetFileName(url)
    Path.Combine(directory, (sprintf "Twitter-%s-%s" user imageName))
    
let imageInCache url user =
    let imagePath = getImagePathFromUrl url user
    File.Exists(imagePath)
    
let downloadImage (url:string) user =
    try 
      let wc = new System.Net.WebClient()
      let image = wc.DownloadData(url)
      let imagePath = getImagePathFromUrl url user
      File.WriteAllBytes(imagePath, wc.DownloadData(url))
    with ex -> 
      printfn "Unable to download image  %s" url
      printfn "%A" ex
    
let getImagePathByParams url user =
    getImagePathFromUrl url user
    
let getImagePath (status:Status.status) =
    let (url, user) = status.UserProfileImage, status.UserName
    getImagePathFromUrl url user
    
let ensureStatusImage (status: Status.status) =
    let (url, user) = status.UserProfileImage, status.UserName
    if not (imageInCache url user) then
        downloadImage url user
    status
let ensureStatusImageNoRet (status: Status.status) =
    ensureStatusImage status |> ignore
let ensureStatusesImages (statuses: Status.status list) =
    statuses |>  List.map ensureStatusImage