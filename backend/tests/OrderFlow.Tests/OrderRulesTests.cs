using OrderFlow.Domain;
using Xunit;

namespace OrderFlow.Tests;

public sealed class OrderRulesTests
{
    [Theory]
    [InlineData("Ada Lovelace", "ABC-01", 1, "")]
    [InlineData("Ada Lovelace", "ABC-01", 100, "")]
    [InlineData(" ", "ABC-01", 2, "customerName")]
    [InlineData("Ada Lovelace", "", 2, "sku")]
    [InlineData("Ada Lovelace", "ABC-01", 0, "quantity")]
    [InlineData("Ada Lovelace", "ABC-01", 101, "quantity")]
    [InlineData(null, null, -5, "customerName,sku,quantity")]
    public void Validate_reports_each_invalid_field(string? customerName, string? sku, int quantity, string expectedFields)
    {
        var errors = OrderRules.Validate(customerName, sku, quantity);

        var expected = expectedFields.Split(',', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expected.Order(), errors.Keys.Order());
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Rejected, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Pending, false)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Rejected, false)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.Rejected, OrderStatus.Confirmed, false)]
    public void CanTransition_only_allows_pending_to_a_final_status(OrderStatus current, OrderStatus target, bool expected)
    {
        Assert.Equal(expected, OrderRules.CanTransition(current, target));
    }
}
