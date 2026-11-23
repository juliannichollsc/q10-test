# OrderFlow backend

Two .NET 10 services that communicate through RabbitMQ. See the [root README](../README.md) for the architecture, decisions, failure handling and how to run the whole system.

## Projects

| Project | Responsibility |
| --- | --- |
| `src/OrderFlow.Domain` | `OrderStatus`, validation and transition rules, integration event records. No dependencies. |
| `src/OrderFlow.Messaging` | RabbitMQ topology and connection options, `OutboxPublisher`, the `MessageConsumer` base class (ack, dead-letter, requeue), and the inbox/outbox SQL helpers |
| `src/Orders.Api` | Minimal API endpoints (orders and the product catalog), `OrdersStore`, `StockResultConsumer`, `OrdersHub` (SignalR) |
| `src/Inventory.Worker` | `InventoryConsumer` and `InventoryStore` (stock reservation) |
| `tests/OrderFlow.Tests` | xUnit rule tests and Testcontainers PostgreSQL integration tests |
| `database/` | `orders.sql` and `inventory.sql` (schema + seed), plus `init.sh`, which Compose runs on the first start |

## Running locally without Docker for the services

Start the infrastructure from the repository root, then run each service with its environment variables:

```bash
docker compose up -d postgres rabbitmq

export RabbitMQ__Host=localhost RabbitMQ__UserName=orderflow RabbitMQ__Password=orderflow_local_only
ConnectionStrings__Orders="Host=localhost;Database=orderflow_orders;Username=orderflow;Password=orderflow_local_only" \
  dotnet run --project backend/src/Orders.Api
ConnectionStrings__Inventory="Host=localhost;Database=orderflow_inventory;Username=orderflow;Password=orderflow_local_only" \
  dotnet run --project backend/src/Inventory.Worker
```

PostgreSQL is not published on the host by default. Add `ports: ["127.0.0.1:5432:5432"]` to the `postgres` service for this workflow.

## Tests

```bash
docker compose run --rm --build backend-tests   # only Docker required
dotnet test backend/OrderFlow.sln               # with the .NET 10 SDK and Docker
```

`DatabaseFixture` starts one PostgreSQL container, creates one database per service, and applies the same `database/*.sql` files that Compose uses. The tests therefore exercise the real schema, constraints and seed data.

## Conventions

- All SQL is parameterized. Transactions are kept short and owned by the store classes.
- Consumers acknowledge only after their transaction commits. `InvalidMessageException` (and malformed JSON) dead-letters a message; any other exception requeues it after a delay.
- Configuration comes only from environment variables. A missing connection string or RabbitMQ credential stops the service at startup.
- Warnings are treated as errors (`Directory.Build.props`).
