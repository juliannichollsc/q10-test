namespace OrderFlow.Messaging;

public static class MessageRoutes
{
    public const string Exchange = "orderflow.events";
    public const string DeadLetterExchange = "orderflow.dead-letter";
    public const string OrderCreated = "order.created";
    public const string StockReserved = "stock.reserved";
    public const string StockRejected = "stock.rejected";
    public const string InventoryQueue = "inventory.order-created";
    public const string OrdersQueue = "orders.stock-results";
    public const string DeadLetterQueue = "orderflow.dead-letters";
}
