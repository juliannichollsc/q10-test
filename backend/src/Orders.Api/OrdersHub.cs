using Microsoft.AspNetCore.SignalR;

namespace OrderFlow.Orders.Api;

public sealed class OrdersHub : Hub
{
    public const string Path = "/hubs/orders";
    public const string OrderChanged = "OrderChanged";
}
