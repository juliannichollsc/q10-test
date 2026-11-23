using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using RabbitMQ.Client;

namespace OrderFlow.Messaging;

public sealed class OutboxPublisher(
    NpgsqlDataSource dataSource,
    RabbitOptions rabbitOptions,
    ILogger<OutboxPublisher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await rabbitOptions.ConnectAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                    stoppingToken);
                await RabbitTopology.DeclareAsync(channel, stoppingToken);
                logger.LogInformation("Outbox publisher connected to RabbitMQ");

                while (!stoppingToken.IsCancellationRequested)
                {
                    while (await PublishNextAsync(channel, stoppingToken))
                    {
                    }

                    await Task.Delay(PollInterval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Outbox publishing failed; pending events stay in the outbox and will be retried");
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task<bool> PublishNextAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using var select = new NpgsqlCommand(
            "select event_id, routing_key, payload::text from outbox where published_at is null order by created_at limit 1 for update skip locked",
            connection, transaction);
        Guid eventId;
        string routingKey;
        string payload;
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken))
                return false;
            eventId = reader.GetGuid(0);
            routingKey = reader.GetString(1);
            payload = reader.GetString(2);
        }

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = eventId.ToString()
        };
        await channel.BasicPublishAsync(MessageRoutes.Exchange, routingKey, mandatory: true, properties,
            Encoding.UTF8.GetBytes(payload), cancellationToken);

        await using var markPublished = new NpgsqlCommand(
            "update outbox set published_at = now() where event_id = @event_id", connection, transaction);
        markPublished.Parameters.AddWithValue("event_id", eventId);
        await markPublished.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Published {RoutingKey} event {EventId}", routingKey, eventId);
        return true;
    }
}
