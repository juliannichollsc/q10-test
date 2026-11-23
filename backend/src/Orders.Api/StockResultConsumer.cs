using Microsoft.AspNetCore.SignalR;
using OrderFlow.Domain;
using OrderFlow.Messaging;

namespace OrderFlow.Orders.Api;

public sealed class StockResultConsumer(
    OrdersStore store,
    IHubContext<OrdersHub> hub,
    RabbitOptions rabbitOptions,
    ILogger<StockResultConsumer> logger) : MessageConsumer(rabbitOptions, logger)
{
    protected override string Queue => MessageRoutes.OrdersQueue;

    protected override async Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var (eventId, orderId, target) = routingKey switch
        {
            MessageRoutes.StockReserved => ToResult(MessageJson.Deserialize<StockReserved>(body)),
            MessageRoutes.StockRejected => ToResult(MessageJson.Deserialize<StockRejected>(body)),
            _ => throw new InvalidMessageException($"Unexpected routing key '{routingKey}'.")
        };

        if (eventId == Guid.Empty || orderId == Guid.Empty)
            throw new InvalidMessageException("Stock result is missing its event or order ID.");

        var updated = await store.ApplyStockResultAsync(eventId, orderId, target, cancellationToken);
        if (updated is null)
        {
            logger.LogInformation("Stock result {EventId} for order {OrderId} was already applied or not applicable", eventId, orderId);
            return;
        }

        logger.LogInformation("Order {OrderId} moved to {Status}", orderId, updated.Status);
        await hub.Clients.All.SendAsync(OrdersHub.OrderChanged, updated, cancellationToken);
    }

    private static (Guid EventId, Guid OrderId, OrderStatus Target) ToResult(StockReserved message) =>
        (message.EventId, message.OrderId, OrderStatus.Confirmed);

    private static (Guid EventId, Guid OrderId, OrderStatus Target) ToResult(StockRejected message) =>
        (message.EventId, message.OrderId, OrderStatus.Rejected);
}
