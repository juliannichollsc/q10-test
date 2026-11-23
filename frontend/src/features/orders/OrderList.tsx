import { useQuery } from "@tanstack/react-query";
import { ordersApi, ordersQueryKey } from "./api";
import type { Order } from "./order-types";
import { useOrderUpdates } from "./use-order-updates";

const POLLING_INTERVAL_MS = 2_000;
const LIVE_SAFETY_INTERVAL_MS = 15_000;

const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: "medium", timeStyle: "short" });

function OrderRow({ order }: { order: Order }) {
  return (
    <tr>
      <td>
        <span className="customer-name">{order.customerName}</span>
        <span className="order-id">{order.id.slice(0, 8)}</span>
      </td>
      <td>{order.sku}</td>
      <td>{order.quantity}</td>
      <td>
        <span className={`status status-${order.status.toLowerCase()}`}>{order.status}</span>
      </td>
      <td>{dateFormat.format(new Date(order.createdAt))}</td>
    </tr>
  );
}

function OrdersTable({ orders }: { orders: Order[] }) {
  if (orders.length === 0) {
    return <p className="empty-state">No orders yet. Create the first one to get started.</p>;
  }

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            <th scope="col">Customer</th>
            <th scope="col">SKU</th>
            <th scope="col">Qty</th>
            <th scope="col">Status</th>
            <th scope="col">Created</th>
          </tr>
        </thead>
        <tbody>
          {orders.map((order) => (
            <OrderRow key={order.id} order={order} />
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function OrderList() {
  const live = useOrderUpdates();
  const orders = useQuery({
    queryKey: ordersQueryKey,
    queryFn: ordersApi.list,
    refetchInterval: live ? LIVE_SAFETY_INTERVAL_MS : POLLING_INTERVAL_MS,
  });

  return (
    <section className="panel" aria-labelledby="orders-title">
      <div className="panel-heading">
        <div>
          <p className="eyebrow">Order status</p>
          <h2 id="orders-title">Recent orders</h2>
        </div>
        <span className="refresh-indicator">
          <span className={live ? "live-dot" : "live-dot live-dot-off"} />
          {live ? "Live updates" : `Polling every ${POLLING_INTERVAL_MS / 1000} s`}
        </span>
      </div>
      {orders.isError && (
        <div className="alert alert-error" role="alert">
          {orders.data ? `Showing the last known status. ${orders.error.message}` : orders.error.message}
        </div>
      )}
      {orders.data ? (
        <OrdersTable orders={orders.data} />
      ) : (
        orders.isPending && <p className="empty-state">Loading orders…</p>
      )}
    </section>
  );
}
