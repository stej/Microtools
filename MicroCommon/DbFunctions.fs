module DbFunctions

open Status

type IStatusesDatabase =
    abstract ReadStatusWithId : int64 -> statusInfo option
    abstract GetLastTimelineId : unit -> int64
    abstract GetLastMentionsId : unit -> int64
    abstract GetLastRetweetsId : unit -> int64
    abstract UpdateLastTimelineId : statusInfo -> unit
    abstract UpdateLastMentionsId : statusInfo -> unit
    abstract UpdateLastRetweetsId : statusInfo -> unit
    abstract SaveStatuses : statusInfo list -> unit
    abstract SaveStatus: statusInfo -> unit
    abstract ReadStatusReplies: int64 -> statusInfo seq

let mutable dbAccess:IStatusesDatabase = 
    { new IStatusesDatabase with
        member x.ReadStatusWithId(_) = failwith "not implemented"
        member x.GetLastTimelineId() = failwith "not implemented"
        member x.GetLastMentionsId() = failwith "not implemented"
        member x.GetLastRetweetsId() = failwith "not implemented"
        member x.UpdateLastTimelineId(_) = failwith "not implemented"
        member x.UpdateLastMentionsId(_) = failwith "not implemented"
        member x.UpdateLastRetweetsId(_) = failwith "not implemented"
        member x.SaveStatuses(_) = failwith "not implemented"
        member x.SaveStatus(_) = failwith "not implemented"
        member x.ReadStatusReplies(_) = failwith "not implemented"
    }