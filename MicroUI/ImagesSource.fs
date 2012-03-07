module ImagesSource

open System.IO
open System
open Utils

let private basedir = AppDomain.CurrentDomain.BaseDirectory
let private directory = Path.Combine(basedir, "images")

let private getImagePathFromUrl url user =
    let imageName = Path.GetFileName(url)
    Path.Combine(directory, (sprintf "Twitter-%s-%s" user imageName))

let private getEmptyImagePath () =
    let path = Path.Combine(basedir, "Twitter-empty.jpg")
    let path2 = Path.Combine(basedir, "Twitter-empty-.jpg")
    if File.Exists(path) then
        path
    else if File.Exists(path2) then
        path2
    else failwith (sprintf "File '%s' nor '%s' not found" path path2)
       
let private imageInCache url user =
    let imagePath = getImagePathFromUrl url user
    File.Exists(imagePath)
    
let private downloadImage (url:string) user =
    async {
        try 
            let! image = AsyncDownloadData (new Uri(url))
            let imagePath = getImagePathFromUrl url user
            File.WriteAllBytes(imagePath, image)
        with ex -> 
            lerrex ex (sprintf "Unable to download image %s" url)
    }

let getImagePath (status:Status.status) =
    let (url, user) = status.UserProfileImage, status.UserName
    if url = Status.emptyStatusUserProfileImage then
        getEmptyImagePath ()
    else
        getImagePathFromUrl url user
    
let asyncEnsureStatusImage (status: Status.status) =
    async {
        let (url, user) = status.UserProfileImage, status.UserName
        if url = Status.emptyStatusUserProfileImage then
            ()
        else if not (imageInCache url user) then
            do! downloadImage url user
        return status
    }
let ensureStatusImage (status: Status.status) =
    asyncEnsureStatusImage status |> Async.RunSynchronously |> ignore

let asyncEnsureStatusesImages (conversations: Status.statusInfo seq) =
    let flat = conversations
                |> StatusFunctions.Flatten
                |> Seq.toList
    async {
        for sInfo in flat do
            let status = sInfo.Status
            let! tmp = asyncEnsureStatusImage status
            ()
    }
let ensureStatusesImages (conversations: Status.statusInfo seq) =
    asyncEnsureStatusesImages conversations |> Async.Start   // todo: same thread?
        
linfop "Images directory is {0}" directory
if not (Directory.Exists(directory)) then 
    linfop "Creating {0}" directory
    Directory.CreateDirectory(directory) |> ignore