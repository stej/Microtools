module DbInterface

open Status

type IStatusesDatabase =
    abstract ReadStatusWithId : int64 -> statusInfo option
    abstract GetLastTimelineId : unit -> int64
    abstract GetLastMentionsId : unit -> int64
    abstract GetLastListItemId : int64 -> int64
    abstract UpdateLastTimelineId : statusInfo -> unit
    abstract UpdateLastMentionsId : statusInfo -> unit
    abstract UpdateLastListItemId : int64 * statusInfo -> unit
    abstract SaveStatuses : statusInfo list -> unit
    abstract SaveStatus: statusInfo -> unit
    abstract ReadStatusReplies: int64 -> statusInfo seq
    abstract Find : (string*string*string) list -> statusInfo seq    // bad interface description

let mutable dbAccess:IStatusesDatabase = 
    { new IStatusesDatabase with
        member x.ReadStatusWithId(_) = failwith "not implemented"
        member x.GetLastTimelineId() = failwith "not implemented"
        member x.GetLastMentionsId() = failwith "not implemented"
        member x.GetLastListItemId(_) = failwith "not implemented"
        member x.UpdateLastTimelineId(_) = failwith "not implemented"
        member x.UpdateLastMentionsId(_) = failwith "not implemented"
        member x.UpdateLastListItemId(_, _) = failwith "not implemented"
        member x.SaveStatuses(_) = failwith "not implemented"
        member x.SaveStatus(_) = failwith "not implemented"
        member x.ReadStatusReplies(_) = failwith "not implemented"
        member x.Find(_) = failwith "not implemented"
    }