namespace Representative.Core;

public sealed class WorkflowObserver
{
    public int CompletionCount { get; private set; }

    public void OnCompleted(object? sender, EventArgs args)
    {
        CompletionCount++;
    }
}
