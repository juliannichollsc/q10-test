# OrderFlow panel

The operations panel registers orders and shows each order's status as it moves from `Pending` to `Confirmed` or `Rejected`. See the [root README](../README.md) for the whole system.

## Stack

- React 19, TypeScript (strict) and Vite.
- **TanStack Query** owns server state: the order list, the create mutation, cache invalidation and refetch intervals.
- **React Hook Form + Zod** own form state and client-side validation.
- **@microsoft/signalr** receives `OrderChanged` pushes from the API. When the hub is unreachable, the list polls every 2 seconds instead.
- In production, Nginx serves the static build and proxies `/api` (HTTP and WebSocket) to the Orders API, so the browser talks to a single origin.

There is no global state library: orders are server state, and the form state is local to the form.

## Structure

```text
src/
  app/                    App shell, QueryClient provider, entry point
  features/orders/
    api.ts                Order and product endpoints, query keys
    order-schema.ts       Zod schema and the form input type
    order-types.ts        Order and product wire types
    OrderForm.tsx         Create form: SKU suggestions, client errors, server field errors, success state
    OrderList.tsx         Status table, error banner that keeps the last known data, live/polling indicator
    use-order-updates.ts  SignalR connection that invalidates the order list on changes
  shared/
    api/http-client.ts    fetch wrapper that turns ProblemDetails into ApiError (message + field errors)
    ui/TextField.tsx      Accessible labelled input with an error linked via aria-describedby
  test/setup.ts           Vitest + Testing Library setup
```

## Error handling

- The catalog (`GET /products`) is shown as buttons under the SKU field and as a native `<datalist>`, and the field still accepts free text. The server is the single authority on whether a SKU exists.
- Client-side validation errors appear under each field before any request is sent.
- Server validation errors (`400` ProblemDetails `errors`) are mapped onto the matching field, for example an unknown SKU, and a summary appears above the button.
- Network failures and `502`–`504` show a plain-language message instead of a browser error.
- If refreshing the list fails, the last known table stays visible under a warning banner.

## Commands

```bash
pnpm install
pnpm dev         # http://localhost:5173, proxies /api to ORDERS_API_URL (default http://localhost:8080)
pnpm test        # Vitest: schema, HTTP client and form behaviour
pnpm typecheck
pnpm build
```

Node.js 22.12+ and pnpm 10 are required. Dependency versions are pinned, and the Docker build installs them with `--frozen-lockfile`.
