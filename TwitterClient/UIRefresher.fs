module UIRefresher

open System
open System.Windows.Threading
open System.Threading
open System.Collections.Concurrent
open UIModel
open Utils
open Status

open System.Windows
open System.Windows.Controls
open System.Windows.Data
open System.Windows.Media

let setAppState window (appStateCtl:TextBlock) state = 
    WpfUtils.dispatchMessage window (fun _ -> appStateCtl.Text <- state)
let setAppState1 window tb format p1 = 
    String.Format(format, [|p1|]) |> setAppState window tb
let setAppState2 window tb (format:string) p1 p2 = 
    String.Format(format, p1, p2) |> setAppState window tb
let setAppStateCount window tb = 
    setAppState window tb (UIState.getAppStrState())

let private fillCache items = 
    let cache = new ConcurrentDictionary<Int64, WpfUtils.conversationNodeControlsInfo>()
    let addToCache (item:WpfUtils.conversationNodeControlsInfo) = 
        let id = item.StatusToDisplay.StatusInfo.LogicalStatusId()
        cache.[id] <- item
    cache.Clear()
    ldbg "CURLS: Clearing ctls cache"
    items |> List.map (fun (mainCtls, statusesCtls) -> statusesCtls)
          |> List.concat
          |> List.iter addToCache
    cache
          
let private resolveUrls (controlsCache:ConcurrentDictionary<Int64, WpfUtils.conversationNodeControlsInfo>) updateCtlCallback = 
    async {
        let started = DateTime.Now
        linfop2 "CURLS: Starting resolving urls from {0}, thread id: {1}" started Thread.CurrentThread.ManagedThreadId
        let expandControlUrls id =
            async { 
                match controlsCache.TryGetValue(id) with
                    | true, v -> 
                           do! v.StatusToDisplay.ExpandUrls()
                           updateCtlCallback v
                           ()
                    | _  -> ()
            }
        let ids = controlsCache.Keys |> Seq.sort |> Seq.toList
        linfop2 "CURLS: Count of ids to resolve: {0}, thread id {1}" ids.Length Thread.CurrentThread.ManagedThreadId 

        for id in ids do
            do! expandControlUrls id
            
        linfop2 "CURLS: resolving urls ended from {0}, thread id: {1}" started Thread.CurrentThread.ManagedThreadId
    }

type private RefreshMsg = 
    | RefreshMsg of UISettingsDescriptor * AsyncReplyChannel<CancellationTokenSource>
    
type RefresherAgent(window : Window, wrapper : WrapPanel, details : StackPanel, appStateCtl : TextBlock) =  
    let mutable cancel : CancellationTokenSource = null

    let cts = ref (new CancellationTokenSource())
    let syncContext = System.Threading.SynchronizationContext.Current
    
    let setCount count (filterStatusInfos: WpfUtils.StatusInfoToDisplay list) = 
        let filtered = filterStatusInfos |> List.fold (fun count curr -> if curr.FilterInfo.Filtered then count+1 else count) 0
        UIState.setCounts count filtered

    let fillDetails uiSettings (statuses: statusInfo list) = 
        // todo: async
        let toDisplay = UIModel.FilterAwareConversation.GetModel uiSettings statuses
        DisplayStatus.FilterAwareConversation.fill details uiSettings toDisplay
    let fillPictures uiSettings (statuses: statusInfo list) =  
        // todo: async
        let toDisplay = UIModel.LitlePreview.GetModel uiSettings statuses
        setCount statuses.Length toDisplay
        setAppStateCount window appStateCtl
        DisplayStatus.LitlePreview.fill wrapper toDisplay
        
    let updateStatusText window uiSettings ctl =
        WpfUtils.dispatchMessage window (fun _ -> DisplayStatus.FilterAwareConversation.updateText uiSettings ctl)
    let updateui uiSettings = 
        async {
            let! list,trees = PreviewsState.userStatusesState.AsyncGetStatuses()
            ldbgp2 "CLI: Count of statuses: {0}/{1}" list.Length trees.Length

            do! ImagesSource.asyncEnsureStatusesImages trees
            ldbg "CLI: Refreshing panels"
                            
            let filterStatusInfos = fillPictures uiSettings list
            let detailsCtls = fillDetails uiSettings trees
                            
            // todo: resolve urls
            let ctls = fillCache detailsCtls
            let textUpdater = updateStatusText window uiSettings
            do! resolveUrls ctls textUpdater
            ldbg "CLI: Refresh done"
        }

    let showDone () =
        UIState.addDone()
        setAppStateCount window appStateCtl
    let showWorking () =
        UIState.addWorking()
        setAppStateCount window appStateCtl
    member x.Refresh(uiSettings, modelGetter) = 
        (!cts).Cancel()
        cts := new CancellationTokenSource()
        let update = async { 
                        showWorking ()

                        do! Async.SwitchToThreadPool()
                        do! modelGetter
                        do! updateui uiSettings
                     }
        Async.StartWithContinuations(update,
                                     (fun _ -> showDone()),
                                     (fun e -> lerrex e "Error in async update"),
                                     (fun canc -> linfo "Cancelled in refresh ..."
                                                  showDone()),
                                     (!cts).Token)

    member x.Refresh(uiSettings) = 
        let update = async {
                        showWorking ()
                        do! updateui uiSettings
                     }
        Async.StartWithContinuations(update,
                                     (fun _ -> showDone()),
                                     (fun e -> lerrex e "Error in async update"),
                                     (fun canc -> linfo "Cancelled in no model refresh..."
                                                  showDone()))