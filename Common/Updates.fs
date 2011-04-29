module Updates
(*
open System
open System.Data.SQLite

type dbVersion = {
    Major: int
    Minor: int
    Updated : DateTime
}

let checkDbVersionTable() =
    StatusDb.useDb (fun conn ->
        match conn.GetSchema("Tables").Select("Table_Name = 'DbVersion'") with
        | [||] -> printfn "Creating table DbVersion"
                  use cmd = conn.CreateCommand(CommandText = "CREATE TABLE DbVersion (Major integer not null, Minor integer not null, Updated integer not null)")
                  cmd.ExecuteNonQuery() |> ignore
                  cmd.CommandText <- (sprintf "INSERT INTO DbVersion(Major, Minor, Updated) VALUES(%d, %d, %d)" 0 0 System.DateTime.MinValue.Ticks)
                  cmd.ExecuteNonQuery() |> ignore
        | _ -> ())
let getDbVersion() =
    StatusDb.useDb (fun conn -> 
       use cmd = conn.CreateCommand(CommandText = "SELECT Major,Minor,Updated FROM DbVersion LIMIT 0,1")
       let r = cmd.ExecuteReader()
       match r.Read() with
       | true -> 
           { Major = Convert.ToInt32(r.["Major"])
             Minor = Convert.ToInt32(r.["Minor"])
             Updated = new DateTime(Convert.ToInt64(r.["Updated"]))
           }
       | _ -> failwith "unexpected: DbVersion doesn't exist"
    )

let execNonquery text =
    StatusDb.useDb (fun conn ->
        use cmd = conn.CreateCommand()
        cmd.CommandText <- text
        cmd.ExecuteNonQuery() |> ignore)

let updateDbVersion major minor =
    execNonquery (sprintf "UPDATE DbVersion SET Major=%d, Minor=%d, Updated=%d" major minor System.DateTime.Now.Ticks)

let updateTo_0_1() =
    printfn "update to 0.1"
    execNonquery "ALTER TABLE Status ADD Inserted integer NOT NULL default -1"
    execNonquery "UPDATE Status SET Inserted = [Date]"
    //execNonquery "CREATE TABLE List (Id integer primary key, UserName varchar(64) not null, Name varchar(64) not null)"
    //execNonquery "CREATE TABLE UsersInList (Id integer primary key, ListId integer, UserName varchar(64) not null)"
    printfn "update to 1.0 done"

let update() =
    checkDbVersionTable()
    let dbVer = getDbVersion()
    let versionIsBelow major minor =
        dbVer.Major < major || (dbVer.Major = major && dbVer.Minor < minor)
    let ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
    printfn "Version: %i-%i-%i-%i" ver.Major ver.Minor ver.Build ver.Revision
    
    if dbVer.Major <> ver.Major || dbVer.Minor <> ver.Minor then
        printfn "Should be updated"
        if versionIsBelow 0 1 then
          updateTo_0_1()
        
    updateDbVersion ver.Major ver.Minor
*)