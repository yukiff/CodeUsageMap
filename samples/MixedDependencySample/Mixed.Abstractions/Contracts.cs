namespace Mixed.Abstractions;

public sealed record OrderRecord(string OrderId, decimal Amount);

public interface IOrderGateway
{
    Task SaveAsync(OrderRecord record);
}

public interface IAuditSink
{
    Task WriteAsync(string message);
}
