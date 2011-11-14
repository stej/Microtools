module DbCommon

open System
open Status
open System.Data.SQLite
open Utils
open StatusFunctions

let private doNothingHandler _ = ()
let private doNothingHandler2 _ _ = ()

let str (rd:SQLiteDataReader) (id:string)  = 
    let o = rd.[id]; 
    match o with | null -> null | s -> s.ToString()
let long (rd:SQLiteDataReader) (id:string) = Convert.ToInt64(rd.[id])
let intt (rd:SQLiteDataReader) (id:string) = Convert.ToInt32(rd.[id])
let date (rd:SQLiteDataReader) (id:string) = new DateTime(long rd id)
let bol (rd:SQLiteDataReader) (id:string)  = Convert.ToBoolean(rd.[id])

let useDb file useFce = 
    use conn = new System.Data.SQLite.SQLiteConnection()
    conn.ConnectionString <- sprintf "Data Source=\"%s\"" file
    conn.Open()
    let result = useFce conn 
    conn.Close()
    result
    
let addCmdParameter (cmd:SQLiteCommand) (name:string) value = 
    cmd.Parameters.Add(new SQLiteParameter(name, (value :> System.Object))) |> ignore

let executeSelect readFce (cmd:SQLiteCommand) = 
    use rd = cmd.ExecuteReader()
    let rec read (rd:SQLiteDataReader) =
        seq {
        if rd.Read() then
            yield readFce rd
            ldbg "reading status"
            yield! read rd
        }
    let result = read rd |> Seq.toList
    rd.Close()
    result