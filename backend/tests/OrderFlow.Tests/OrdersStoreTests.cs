using OrderFlow.Domain;
using OrderFlow.Orders.Api;
using Xunit;

namespace OrderFlow.Tests;

[Collection(DatabaseCollection.Name)]
public sealed class OrdersStoreTests(DatabaseFixture database)
{
    [Fact]
    public async Task Stock_results_move_a_pending_order_once_and_ignore_duplicates_and_late_results()
    {
        var store = new OrdersStore(database.Orders);
        var order = await store.CreateAsync("Ada Lovelace", "ABC-02", 2, CancellationToken.None);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Pending, order.Status);

        var reservedEventId = Guid.NewGuid();
        var confirmed = await store.ApplyStockResultAsync(reservedEventId, order.Id, OrderStatus.Confirmed, CancellationToken.None);
        var duplicate = await store.ApplyStockResultAsync(reservedEventId, order.Id, OrderStatus.Confirmed, CancellationToken.None);
        var late = await store.ApplyStockResultAsync(Guid.NewGuid(), order.Id, OrderStatus.Rejected, CancellationToken.None);

        Assert.Equal(OrderStatus.Confirmed, confirmed?.Status);
        Assert.Null(duplicate);
        Assert.Null(late);
        Assert.Equal(OrderStatus.Confirmed, (await store.FindAsync(order.Id, CancellationToken.None))?.Status);
        Assert.Equal("order.created", await database.ScalarAsync<string>(database.Orders,
            "select routing_key from outbox where payload->>'orderId' = @order_id", ("order_id", order.Id.ToString())));
    }
}
