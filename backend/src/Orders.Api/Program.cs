using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using OrderFlow.Domain;
using OrderFlow.Messaging;
using OrderFlow.Orders.Api;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Orders")
    ?? throw new InvalidOperationException("ConnectionStrings__Orders is required.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton(RabbitOptions.FromConfiguration(builder.Configuration, "orders-api"));
builder.Services.AddSingleton<OrdersStore>();
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddHostedService<StockResultConsumer>();
builder.Services.AddSignalR()
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError
});
app.UseStatusCodePages();
app.MapOpenApi();

var orders = app.MapGroup("/orders").WithTags("Orders");

orders.MapPost("", async Task<Results<Created<OrderResponse>, ValidationProblem>> (
    CreateOrderRequest request, OrdersStore store, IHubContext<OrdersHub> hub, CancellationToken cancellationToken) =>
{
    var errors = OrderRules.Validate(request.CustomerName, request.Sku, request.Quantity);
    if (errors.Count > 0)
        return TypedResults.ValidationProblem(errors);

    var order = await store.CreateAsync(request.CustomerName!.Trim(), request.Sku!.Trim(), request.Quantity, cancellationToken);
    if (order is null)
        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["sku"] = [$"SKU '{request.Sku.Trim()}' does not exist in the product catalog."]
        });

    await hub.Clients.All.SendAsync(OrdersHub.OrderChanged, order, cancellationToken);
    return TypedResults.Created($"/orders/{order.Id}", order);
})
    .WithName("CreateOrder")
    .WithSummary("Create a pending order and queue an OrderCreated event");

orders.MapGet("", async (OrdersStore store, CancellationToken cancellationToken) =>
        TypedResults.Ok(await store.ListAsync(cancellationToken)))
    .WithName("ListOrders")
    .WithSummary("List the most recent orders with their current status");

orders.MapGet("/{id:guid}", async Task<Results<Ok<OrderResponse>, NotFound>> (
    Guid id, OrdersStore store, CancellationToken cancellationToken) =>
        await store.FindAsync(id, cancellationToken) is { } order ? TypedResults.Ok(order) : TypedResults.NotFound())
    .WithName("GetOrder")
    .WithSummary("Get an order by ID");

app.MapGet("/products", async (OrdersStore store, CancellationToken cancellationToken) =>
        TypedResults.Ok(await store.ListProductsAsync(cancellationToken)))
    .WithTags("Products")
    .WithName("ListProducts")
    .WithSummary("List the catalog products that can be ordered");

app.MapHub<OrdersHub>(OrdersHub.Path);

app.MapGet("/healthz", async (NpgsqlDataSource dataSource, CancellationToken cancellationToken) =>
{
    await using var command = dataSource.CreateCommand("select 1");
    await command.ExecuteScalarAsync(cancellationToken);
    return TypedResults.Ok(new { status = "healthy" });
}).ExcludeFromDescription();

app.MapGet("/livez", () => TypedResults.Ok(new { status = "alive" })).ExcludeFromDescription();

app.Run();
