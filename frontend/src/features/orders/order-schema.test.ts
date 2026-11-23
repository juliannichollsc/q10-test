import { describe, expect, it } from "vitest";
import { createOrderSchema } from "./order-schema";

function firstError(input: unknown) {
  const result = createOrderSchema.safeParse(input);
  return result.success ? undefined : result.error.issues[0];
}

describe("createOrderSchema", () => {
  it("accepts a valid order and trims text", () => {
    expect(createOrderSchema.parse({ customerName: "  Ada  ", sku: " ABC-01 ", quantity: 3 })).toEqual({
      customerName: "Ada",
      sku: "ABC-01",
      quantity: 3,
    });
  });

  it.each([
    [{ customerName: "   ", sku: "ABC-01", quantity: 1 }, "customerName", "Customer name is required."],
    [{ customerName: "Ada", sku: "", quantity: 1 }, "sku", "SKU is required."],
    [{ customerName: "Ada", sku: "ABC-01", quantity: Number.NaN }, "quantity", "Quantity is required."],
    [{ customerName: "Ada", sku: "ABC-01", quantity: 0 }, "quantity", "Quantity must be at least 1."],
    [{ customerName: "Ada", sku: "ABC-01", quantity: 101 }, "quantity", "Quantity cannot exceed 100."],
    [{ customerName: "Ada", sku: "ABC-01", quantity: 1.5 }, "quantity", "Quantity must be a whole number."],
  ])("rejects %j", (input, field, message) => {
    expect(firstError(input)).toMatchObject({ path: [field], message });
  });
});
