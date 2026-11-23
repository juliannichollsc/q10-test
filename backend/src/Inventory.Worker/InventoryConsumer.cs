using OrderFlow.Domain;
using OrderFlow.Messaging;

namespace OrderFlow.Inventory.Worker;

public sealed class InventoryConsumer(
    InventoryStore store,
    RabbitOptions rabbitOptions,
    ILogger<InventoryConsumer> logger) : MessageConsumer(rabbitOptions, logger)
{
    protected override string Queue => MessageRoutes.InventoryQueue;

    protected override async Task HandleAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var order = MessageJson.Deserialize<OrderCreated>(body);
        if (order.EventId == Guid.Empty || order.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(order.Sku) ||
            order.Quantity is < OrderRules.MinQuantity or > OrderRules.MaxQuantity)
            throw new InvalidMessageException($"OrderCreated event {order.EventId} has invalid fields.");

        var outcome = await store.ReserveAsync(order, cancellationToken);
        logger.LogInformation("OrderCreated {EventId} for order {OrderId}: {Outcome}", order.EventId, order.OrderId, outcome);
    }
}
