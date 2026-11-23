using Npgsql;
using OrderFlow.Domain;
using OrderFlow.Messaging;

namespace OrderFlow.Inventory.Worker;

public enum ReservationOutcome
{
    Reserved,
    Rejected,
    Duplicate
}

public sealed class InventoryStore(NpgsqlDataSource dataSource)
{
    public async Task<ReservationOutcome> ReserveAsync(OrderCreated order, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await TransactionalMessaging.TryRecordInboxAsync(connection, transaction, order.EventId, cancellationToken))
            return ReservationOutcome.Duplicate;

        await using var reserveCommand = new NpgsqlCommand(
            "update stock set available = available - @quantity where sku = @sku and available >= @quantity",
            connection, transaction);
        reserveCommand.Parameters.AddWithValue("quantity", order.Quantity);
        reserveCommand.Parameters.AddWithValue("sku", order.Sku);
        var reserved = await reserveCommand.ExecuteNonQueryAsync(cancellationToken) == 1;

        var eventId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        if (reserved)
            await TransactionalMessaging.AddToOutboxAsync(connection, transaction, eventId, MessageRoutes.StockReserved,
                new StockReserved(eventId, order.OrderId, occurredAt), cancellationToken);
        else
            await TransactionalMessaging.AddToOutboxAsync(connection, transaction, eventId, MessageRoutes.StockRejected,
                new StockRejected(eventId, order.OrderId, $"Insufficient stock for {order.Sku}.", occurredAt), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return reserved ? ReservationOutcome.Reserved : ReservationOutcome.Rejected;
    }
}
