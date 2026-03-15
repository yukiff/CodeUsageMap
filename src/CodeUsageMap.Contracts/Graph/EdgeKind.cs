namespace CodeUsageMap.Contracts.Graph;

public enum EdgeKind
{
    DirectCall = 0,
    InterfaceDispatch,
    DiResolvedCall,
    Reference,
    Implements,
    Overrides,
    InjectedByDi,
    InstantiatedBy,
    ContainsSubscription,
    EventSubscription,
    EventUnsubscription,
    EventHandlerTarget,
    EventRaise,
    EventDispatchEstimated,
    UnknownDynamicDispatch,
}
