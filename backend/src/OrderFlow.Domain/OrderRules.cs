namespace OrderFlow.Domain;

public static class OrderRules
{
    public const int MinQuantity = 1;
    public const int MaxQuantity = 100;

    public static Dictionary<string, string[]> Validate(string? customerName, string? sku, int quantity)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(customerName))
            errors["customerName"] = ["Customer name is required."];

        if (string.IsNullOrWhiteSpace(sku))
            errors["sku"] = ["SKU is required."];

        if (quantity is < MinQuantity or > MaxQuantity)
            errors["quantity"] = [$"Quantity must be between {MinQuantity} and {MaxQuantity}."];

        return errors;
    }

    public static bool CanTransition(OrderStatus current, OrderStatus target) =>
        current == OrderStatus.Pending && target is OrderStatus.Confirmed or OrderStatus.Rejected;
}
