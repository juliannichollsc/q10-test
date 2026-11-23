using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace OrderFlow.Messaging;

public sealed record RabbitOptions(string Host, int Port, string UserName, string Password, string ClientName)
{
    public static RabbitOptions FromConfiguration(IConfiguration configuration, string clientName)
    {
        var section = configuration.GetSection("RabbitMQ");
        return new RabbitOptions(
            Required(section, "Host"),
            int.TryParse(section["Port"], out var port) ? port : 5672,
            Required(section, "UserName"),
            Required(section, "Password"),
            clientName);
    }

    public Task<IConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = Host,
            Port = Port,
            UserName = UserName,
            Password = Password,
            ClientProvidedName = ClientName,
            AutomaticRecoveryEnabled = false
        };
        return factory.CreateConnectionAsync(cancellationToken);
    }

    private static string Required(IConfigurationSection section, string key) =>
        section[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"RabbitMQ__{key} is required.");
}
