using Npgsql;
using NpgsqlTypes;

namespace OrderFlow.Messaging;

public static class TransactionalMessaging
{
    public static async Task<bool> TryRecordInboxAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid eventId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "insert into inbox (event_id) values (@event_id) on conflict do nothing", connection, transaction);
        command.Parameters.AddWithValue("event_id", eventId);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    public static async Task AddToOutboxAsync<T>(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid eventId, string routingKey, T message, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "insert into outbox (event_id, routing_key, payload) values (@event_id, @routing_key, @payload)", connection, transaction);
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("routing_key", routingKey);
        command.Parameters.Add("payload", NpgsqlDbType.Jsonb).Value = MessageJson.Serialize(message);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
