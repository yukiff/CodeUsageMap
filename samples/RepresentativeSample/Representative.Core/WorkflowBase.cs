namespace Representative.Core;

public abstract class WorkflowBase : IWorkflow
{
    public event EventHandler? Completed;

    public virtual Task ExecuteAsync()
    {
        RaiseCompleted();
        return Task.CompletedTask;
    }

    protected void RaiseCompleted()
    {
        Completed?.Invoke(this, EventArgs.Empty);
    }
}
