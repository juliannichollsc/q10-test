using OrderFlow.Domain;

namespace OrderFlow.Orders.Api;

public sealed record CreateOrderRequest(string? CustomerName, string? Sku, int Quantity);

public sealed record OrderResponse(Guid Id, string CustomerName, string Sku, int Quantity, OrderStatus Status, DateTimeOffset CreatedAt);

public sealed record ProductResponse(string Sku, string Name);
