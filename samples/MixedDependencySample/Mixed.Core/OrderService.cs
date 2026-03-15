using Mixed.Abstractions;

namespace Mixed.Core;

public sealed class OrderService
{
    private readonly IOrderGateway _gateway;
    private readonly IAuditSink _auditSink;

    public OrderService(IOrderGateway gateway, IAuditSink auditSink)
    {
        _gateway = gateway;
        _auditSink = auditSink;
    }

    public event EventHandler? Submitted;

    public async Task SubmitAsync(OrderRecord record)
    {
        await _gateway.SaveAsync(record);
        await _auditSink.WriteAsync($"submit:{record.OrderId}");
        Submitted?.Invoke(this, EventArgs.Empty);
        await NotifyAsync(record);
    }

    public Task NotifyAsync(OrderRecord record)
    {
        return _auditSink.WriteAsync($"notify:{record.OrderId}");
    }
}
