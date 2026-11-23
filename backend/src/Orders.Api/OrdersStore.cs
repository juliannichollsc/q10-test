using Npgsql;
using OrderFlow.Domain;
using OrderFlow.Messaging;

namespace OrderFlow.Orders.Api;

public sealed class OrdersStore(NpgsqlDataSource dataSource)
{
    private const int ListLimit = 100;
    private const string OrderColumns = "id, customer_name, sku, quantity, status, created_at";

    public async Task<OrderResponse?> CreateAsync(string customerName, string sku, int quantity, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var catalogCommand = new NpgsqlCommand(
            "select exists(select 1 from catalog_products where sku = @sku)", connection, transaction);
        catalogCommand.Parameters.AddWithValue("sku", sku);
        if (await catalogCommand.ExecuteScalarAsync(cancellationToken) is not true)
            return null;

        var order = new OrderResponse(Guid.NewGuid(), customerName, sku, quantity, OrderStatus.Pending, DateTimeOffset.UtcNow);
        await using var insertCommand = new NpgsqlCommand(
            $"insert into orders ({OrderColumns}) values (@id, @customer_name, @sku, @quantity, @status, @created_at)",
            connection, transaction);
        insertCommand.Parameters.AddWithValue("id", order.Id);
        insertCommand.Parameters.AddWithValue("customer_name", order.CustomerName);
        insertCommand.Parameters.AddWithValue("sku", order.Sku);
        insertCommand.Parameters.AddWithValue("quantity", order.Quantity);
        insertCommand.Parameters.AddWithValue("status", order.Status.ToString());
        insertCommand.Parameters.AddWithValue("created_at", order.CreatedAt);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);

        var orderCreated = new OrderCreated(Guid.NewGuid(), order.Id, order.Sku, order.Quantity, order.CreatedAt);
        await TransactionalMessaging.AddToOutboxAsync(connection, transaction, orderCreated.EventId,
            MessageRoutes.OrderCreated, orderCreated, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return order;
    }

    public async Task<OrderResponse[]> ListAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"select {OrderColumns} from orders order by created_at desc limit {ListLimit}");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var orders = new List<OrderResponse>();
        while (await reader.ReadAsync(cancellationToken))
            orders.Add(ReadOrder(reader));
        return [.. orders];
    }

    public async Task<ProductResponse[]> ListProductsAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("select sku, name from catalog_products order by sku");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var products = new List<ProductResponse>();
        while (await reader.ReadAsync(cancellationToken))
            products.Add(new ProductResponse(reader.GetString(0), reader.GetString(1)));
        return [.. products];
    }

    public async Task<OrderResponse?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand($"select {OrderColumns} from orders where id = @id");
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadOrder(reader) : null;
    }

    public async Task<OrderResponse?> ApplyStockResultAsync(Guid eventId, Guid orderId, OrderStatus target, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        if (!await TransactionalMessaging.TryRecordInboxAsync(connection, transaction, eventId, cancellationToken))
            return null;

        OrderResponse? order;
        await using (var selectCommand = new NpgsqlCommand(
            $"select {OrderColumns} from orders where id = @id for update", connection, transaction))
        {
            selectCommand.Parameters.AddWithValue("id", orderId);
            await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
            order = await reader.ReadAsync(cancellationToken) ? ReadOrder(reader) : null;
        }

        if (order is null || !OrderRules.CanTransition(order.Status, target))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        await using var updateCommand = new NpgsqlCommand(
            "update orders set status = @status where id = @id", connection, transaction);
        updateCommand.Parameters.AddWithValue("status", target.ToString());
        updateCommand.Parameters.AddWithValue("id", orderId);
        await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return order with { Status = target };
    }

    private static OrderResponse ReadOrder(NpgsqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3),
        Enum.Parse<OrderStatus>(reader.GetString(4)), reader.GetFieldValue<DateTimeOffset>(5));
}
