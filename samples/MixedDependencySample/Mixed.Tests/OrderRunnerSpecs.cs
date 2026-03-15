using Mixed.Abstractions;
using Mixed.App;
using Mixed.Core;

namespace Mixed.Tests;

public sealed class OrderRunnerSpecs
{
    public async Task Exercises_runner(OrderRunner runner)
    {
        await runner.RunAsync();
    }

    public Task Calls_core_directly(OrderService service)
    {
        return service.NotifyAsync(new OrderRecord("T-001", 1m));
    }
}
