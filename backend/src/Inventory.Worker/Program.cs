using Npgsql;
using OrderFlow.Inventory.Worker;
using OrderFlow.Messaging;

var builder = Host.CreateApplicationBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Inventory")
    ?? throw new InvalidOperationException("ConnectionStrings__Inventory is required.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton(RabbitOptions.FromConfiguration(builder.Configuration, "inventory-worker"));
builder.Services.AddSingleton<InventoryStore>();
builder.Services.AddHostedService<InventoryConsumer>();
builder.Services.AddHostedService<OutboxPublisher>();

await builder.Build().RunAsync();
