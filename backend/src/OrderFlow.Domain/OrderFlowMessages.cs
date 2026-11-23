namespace OrderFlow.Domain;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Rejected
}

public sealed record OrderCreated(Guid EventId, Guid OrderId, string Sku, int Quantity, DateTimeOffset OccurredAt);
public sealed record StockReserved(Guid EventId, Guid OrderId, DateTimeOffset OccurredAt);
public sealed record StockRejected(Guid EventId, Guid OrderId, string Reason, DateTimeOffset OccurredAt);
