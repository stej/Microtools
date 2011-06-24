module ipy

open System
open System.Collections.Generic
open System.IO
open Microsoft.Scripting.Hosting
open System.Text

let private runtime = IronPython.Hosting.Python.CreateRuntime();
let engine = runtime.GetEngine("IronPython");

engine.SetSearchPaths([|AppDomain.CurrentDomain.BaseDirectory
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lib")
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\Lib")|])

AppDomain.CurrentDomain.GetAssemblies() |> Array.iter runtime.LoadAssembly

let createScope (values:Dictionary<string, obj>) =
  let scope = engine.Runtime.CreateScope()
  if values <> null then
    for par in values do 
        printfn "Setting object (%s - %A) to Ipy engine" par.Key par.Value
        scope.SetVariable(par.Key, par.Value)
  scope
  
let executeIpy (script:string) (values:Dictionary<string, obj>) =
    let scope = createScope values
    engine.Execute(script, scope)