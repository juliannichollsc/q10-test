import { request } from "../../shared/api/http-client";
import type { CreateOrderInput } from "./order-schema";
import type { Order, Product } from "./order-types";

export const ordersQueryKey = ["orders"] as const;
export const productsQueryKey = ["products"] as const;

export const productsApi = {
  list: () => request<Product[]>("/products"),
};

export const ordersApi = {
  list: () => request<Order[]>("/orders"),
  create: (input: CreateOrderInput) =>
    request<Order>("/orders", {
      method: "POST",
      body: JSON.stringify(input),
    }),
};
