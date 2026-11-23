using OrderFlow.Domain;
using OrderFlow.Inventory.Worker;
using Xunit;

namespace OrderFlow.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class InventoryStoreTests(DatabaseFixture database)
{
    [Fact]
    public async Task Same_event_processed_twice_decrements_stock_once()
    {
        var store = new InventoryStore(database.Inventory);
        var before = await AvailableAsync("ABC-01");
        var orderCreated = new OrderCreated(Guid.NewGuid(), Guid.NewGuid(), "ABC-01", 3, DateTimeOffset.UtcNow);

        var concurrent = await Task.WhenAll(
            store.ReserveAsync(orderCreated, CancellationToken.None),
            store.ReserveAsync(orderCreated, CancellationToken.None));
        var redelivered = await store.ReserveAsync(orderCreated, CancellationToken.None);

        Assert.Single(concurrent, outcome => outcome == ReservationOutcome.Reserved);
        Assert.Single(concurrent, outcome => outcome == ReservationOutcome.Duplicate);
        Assert.Equal(ReservationOutcome.Duplicate, redelivered);
        Assert.Equal(before - 3, await AvailableAsync("ABC-01"));
        Assert.Equal(1L, await ResultEventsAsync(orderCreated.OrderId));
    }

    [Fact]
    public async Task Insufficient_stock_is_rejected_without_changing_stock()
    {
        var store = new InventoryStore(database.Inventory);
        var before = await AvailableAsync("ABC-03");
        var orderCreated = new OrderCreated(Guid.NewGuid(), Guid.NewGuid(), "ABC-03", before + 1, DateTimeOffset.UtcNow);

        var outcome = await store.ReserveAsync(orderCreated, CancellationToken.None);

        Assert.Equal(ReservationOutcome.Rejected, outcome);
        Assert.Equal(before, await AvailableAsync("ABC-03"));
        Assert.Equal("stock.rejected", await database.ScalarAsync<string>(database.Inventory,
            "select routing_key from outbox where payload->>'orderId' = @order_id", ("order_id", orderCreated.OrderId.ToString())));
    }

    private Task<int> AvailableAsync(string sku) =>
        database.ScalarAsync<int>(database.Inventory, "select available from stock where sku = @sku", ("sku", sku));

    private Task<long> ResultEventsAsync(Guid orderId) =>
        database.ScalarAsync<long>(database.Inventory,
            "select count(*) from outbox where payload->>'orderId' = @order_id", ("order_id", orderId.ToString()));
}
