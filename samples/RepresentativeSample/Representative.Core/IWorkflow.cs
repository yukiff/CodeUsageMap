namespace Representative.Core;

public interface IWorkflow
{
    event EventHandler? Completed;

    Task ExecuteAsync();
}
