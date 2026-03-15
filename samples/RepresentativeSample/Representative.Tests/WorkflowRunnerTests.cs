using Representative.App;
using Representative.Core;

namespace Representative.Tests;

public sealed class WorkflowRunnerTests
{
    public async Task Run_executes_workflow(IWorkflow workflow)
    {
        var observer = new WorkflowObserver();
        var runner = new WorkflowRunner(workflow, observer);
        await runner.RunAsync();
    }
}
