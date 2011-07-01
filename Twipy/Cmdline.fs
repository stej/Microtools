module Cmdline

open Mono.Options

type parsedOptions = {
    Help : bool
    NoGui : bool
    Run : bool
    OutputScript: bool
    ScriptFile : string option
    Script : string option
}

let help = ref false
let nogui = ref false
let scriptFile = ref None
let script = ref None
let run = ref false
let outputScript = ref false

let getOptionset () =
    let opts = new OptionSet()
    opts.Add("help|?|h", "Show me the help", fun o -> help := true) |> ignore
    opts.Add("nogui", "Don't show the gui", fun o -> nogui := true) |> ignore
    opts.Add("outputScript", "Show the selected script", fun o -> outputScript := true) |> ignore
    opts.Add("f=|file=", "Script file", fun o -> scriptFile := Some(o)) |> ignore
    opts.Add("script=|s=", "Script definition", fun o -> script := Some(o)) |> ignore
    opts.Add("r|run", "Run the script. Applies only if gui should be shown", fun o -> run := true) |> ignore
    opts

let parseCommandLine args  = 
    let opts = getOptionset ()
    opts.Parse(args) |> ignore
    if (!scriptFile).IsSome && (!script).IsSome then
        failwith "Either script or file may be defined"

    { Help = !help
      NoGui = !nogui
      Run = !run
      OutputScript = !outputScript
      ScriptFile = !scriptFile
      Script = !script }