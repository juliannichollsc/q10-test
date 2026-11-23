export type OrderStatus = "Pending" | "Confirmed" | "Rejected";

export type Order = {
  id: string;
  customerName: string;
  sku: string;
  quantity: number;
  status: OrderStatus;
  createdAt: string;
};

export type Product = {
  sku: string;
  name: string;
};
