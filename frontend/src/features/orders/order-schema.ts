import { z } from "zod";

export const createOrderSchema = z.object({
  customerName: z.string().trim().min(1, "Customer name is required."),
  sku: z.string().trim().min(1, "SKU is required."),
  quantity: z
    .number({ error: "Quantity is required." })
    .int("Quantity must be a whole number.")
    .min(1, "Quantity must be at least 1.")
    .max(100, "Quantity cannot exceed 100."),
});

export type CreateOrderInput = z.infer<typeof createOrderSchema>;
