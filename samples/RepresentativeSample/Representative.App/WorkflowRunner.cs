using Representative.Core;

namespace Representative.App;

public sealed class WorkflowRunner
{
    private readonly IWorkflow _workflow;
    private readonly WorkflowObserver _observer;

    public WorkflowRunner(IWorkflow workflow, WorkflowObserver observer)
    {
        _workflow = workflow;
        _observer = observer;
        _workflow.Completed += _observer.OnCompleted;
    }

    public async Task RunAsync()
    {
        await _workflow.ExecuteAsync();
    }

    public void StopObserving()
    {
        _workflow.Completed -= _observer.OnCompleted;
    }
}
