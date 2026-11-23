using System.Text.Json;

namespace OrderFlow.Messaging;

public static class MessageJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T message) => JsonSerializer.Serialize(message, Options);

    public static T Deserialize<T>(ReadOnlyMemory<byte> body) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body.Span, Options)
                ?? throw new InvalidMessageException($"{typeof(T).Name} payload is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidMessageException($"{typeof(T).Name} payload is not valid JSON.", exception);
        }
    }
}

public sealed class InvalidMessageException(string message, Exception? innerException = null)
    : Exception(message, innerException);
