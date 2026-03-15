using Mixed.Abstractions;
using Mixed.Core;
using Mixed.Infrastructure;

namespace Mixed.App;

public sealed class Bootstrapper
{
    public OrderRunner BuildRunner()
    {
        var services = new ServiceRegistry();
        services.AddMixedDependencies();

        var gateway = new SqlOrderGateway();
        var replica = new ReplicaOrderGateway();
        var audit = new ConsoleAuditSink();
        var service = new OrderService(gateway, audit);
        var subscriber = new MetricsSubscriber();
        subscriber.Observe(service);
        _ = replica.SaveAsync(new OrderRecord("warmup", 0m));
        return new OrderRunner(service, subscriber);
    }
}
