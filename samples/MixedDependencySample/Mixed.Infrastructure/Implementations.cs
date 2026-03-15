using Mixed.Abstractions;
using Mixed.Core;

namespace Mixed.Infrastructure;

public sealed class SqlOrderGateway : IOrderGateway
{
    public Task SaveAsync(OrderRecord record)
    {
        return Task.CompletedTask;
    }
}

public sealed class ReplicaOrderGateway : IOrderGateway
{
    public Task SaveAsync(OrderRecord record)
    {
        return Task.CompletedTask;
    }
}

public sealed class ConsoleAuditSink : IAuditSink
{
    public Task WriteAsync(string message)
    {
        return Task.CompletedTask;
    }
}

public sealed class MetricsSubscriber
{
    public void Observe(OrderService service)
    {
        service.Submitted += OnSubmitted;
    }

    public void Stop(OrderService service)
    {
        service.Submitted -= OnSubmitted;
    }

    private void OnSubmitted(object? sender, EventArgs args)
    {
    }
}

public static class MixedServiceRegistration
{
    public static ServiceRegistry AddMixedDependencies(this ServiceRegistry services)
    {
        services.AddScoped<IOrderGateway, SqlOrderGateway>();
        services.AddScoped<IOrderGateway, ReplicaOrderGateway>();
        services.AddTransient(typeof(IAuditSink), typeof(ConsoleAuditSink));
        return services;
    }
}
