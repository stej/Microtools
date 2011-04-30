module ImagesSource

open System.IO
open System
open Utils

let private directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images")

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
      lerrp "Unable to download image {0}" url
      lerrp "{0}" ex
    
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
    statuses |> List.map ensureStatusImage
    
linfop "Images directory is {0}" directory
if not (Directory.Exists(directory)) then 
    linfop "Creating {0}" directory
    Directory.CreateDirectory(directory) |> ignore