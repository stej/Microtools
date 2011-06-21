module DbFunctions

open Status

type IStatusesDatabase =
    abstract ReadStatusWithId : int64 -> status option
    abstract GetLastTimelineId : unit -> int64
    abstract GetLastMentionsId : unit -> int64
    abstract GetLastRetweetsId : unit -> int64
    abstract UpdateLastTimelineId : status -> unit
    abstract UpdateLastMentionsId : status -> unit
    abstract UpdateLastRetweetsId : status -> unit
    abstract SaveStatuses : (status * StatusSource) list -> unit
    abstract SaveStatus: StatusSource * status -> unit
    abstract ReadStatusReplies: int64 -> status seq

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
        member x.SaveStatus(_,_) = failwith "not implemented"
        member x.ReadStatusReplies(_) = failwith "not implemented"
    }