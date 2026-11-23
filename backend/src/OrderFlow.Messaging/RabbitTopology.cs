using RabbitMQ.Client;

namespace OrderFlow.Messaging;

public static class RabbitTopology
{
    private const string DeadLetterRoutingKey = "dead";

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(MessageRoutes.Exchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(MessageRoutes.DeadLetterExchange, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);

        var deadLetterArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = MessageRoutes.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = DeadLetterRoutingKey
        };
        await DeclareQueueAsync(channel, MessageRoutes.InventoryQueue, deadLetterArguments, cancellationToken);
        await DeclareQueueAsync(channel, MessageRoutes.OrdersQueue, deadLetterArguments, cancellationToken);
        await DeclareQueueAsync(channel, MessageRoutes.DeadLetterQueue, null, cancellationToken);

        await channel.QueueBindAsync(MessageRoutes.InventoryQueue, MessageRoutes.Exchange, MessageRoutes.OrderCreated, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(MessageRoutes.OrdersQueue, MessageRoutes.Exchange, MessageRoutes.StockReserved, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(MessageRoutes.OrdersQueue, MessageRoutes.Exchange, MessageRoutes.StockRejected, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(MessageRoutes.DeadLetterQueue, MessageRoutes.DeadLetterExchange, DeadLetterRoutingKey, cancellationToken: cancellationToken);
    }

    private static Task<QueueDeclareOk> DeclareQueueAsync(IChannel channel, string queue,
        IDictionary<string, object?>? arguments, CancellationToken cancellationToken) =>
        channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments, cancellationToken: cancellationToken);
}
