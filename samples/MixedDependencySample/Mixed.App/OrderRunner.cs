using Mixed.Abstractions;
using Mixed.Core;
using Mixed.Infrastructure;

namespace Mixed.App;

public sealed class OrderRunner
{
    private readonly OrderService _service;
    private readonly MetricsSubscriber _subscriber;

    public OrderRunner(OrderService service, MetricsSubscriber subscriber)
    {
        _service = service;
        _subscriber = subscriber;
    }

    public async Task RunAsync()
    {
        await _service.SubmitAsync(new OrderRecord("A-100", 12.5m));
    }

    public void Stop()
    {
        _subscriber.Stop(_service);
    }
}
