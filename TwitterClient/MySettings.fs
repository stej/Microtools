module ClientSettings

open System
open System.Drawing
open System.Configuration

type MySettings() = 
  inherit ApplicationSettingsBase()

  [<UserScopedSettingAttribute()>]
  [<DefaultSettingValueAttribute("@idontwanttoseethisuser")>]
  member this.LastFilter
    with get() = this.Item("LastFilter") :?> string
    and set(value : string) = this.Item("LastFilter") <- value

  [<UserScopedSettingAttribute()>]
  [<DefaultSettingValueAttribute("true")>]
  member this.PreviewPanelVisible
    with get() = this.Item("PreviewPanelVisible") :?> bool
    and set(value : bool) = this.Item("PreviewPanelVisible") <- value

  [<UserScopedSettingAttribute()>]
  [<DefaultSettingValueAttribute("true")>]
  member this.ShowFilteredItems
    with get() = this.Item("ShowFilteredItems") :?> bool
    and set(value : bool) = this.Item("ShowFilteredItems") <- value

  [<UserScopedSettingAttribute()>]
  [<DefaultSettingValueAttribute("false")>]
  member this.OnTop
    with get() = this.Item("OnTop") :?> bool
    and set(value : bool) = this.Item("OnTop") <- value

  [<UserScopedSettingAttribute()>]
  [<DefaultSettingValueAttribute("100")>]
  member this.WindowTop
    with get() = this.Item("WindowTop") :?> double
    and set(value : double) = this.Item("WindowTop") <- value

  [<UserScopedSettingAttribute()>]
  [<DefaultSettingValueAttribute("100")>]
  member this.WindowLeft
    with get() = this.Item("WindowLeft") :?> double
    and set(value : double) = this.Item("WindowLeft") <- value

  [<UserScopedSettingAttribute()>]
  [<DefaultSettingValueAttribute("400")>]
  member this.WindowHeight
    with get() = this.Item("WindowHeight") :?> double
    and set(value : double) = this.Item("WindowHeight") <- value

  [<UserScopedSettingAttribute()>]
  [<DefaultSettingValueAttribute("500")>]
  member this.WindowWidth
    with get() = this.Item("WindowWidth") :?> double
    and set(value : double) = this.Item("WindowWidth") <- value

  (*[<ApplicationScopedSettingAttribute()>]
  [<DefaultSettingValueAttribute("0, 0")>]
  member this.Position
    with get() = this.Item("Position") :?> Point
    and set(value : Point) = this.Item("Position") <- value*)