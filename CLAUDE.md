# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
dotnet build Katalog-Backend.slnx
dotnet test Katalog-Backend.slnx --configuration Release              # full suite = CI gate
dotnet test Katalog-Backend.Tests --filter FullyQualifiedName~OrderServiceTests
cp .env.example .env && docker compose up -d                          # full stack (API, consumer, SQL, Service Bus emulator, Grafana)
./scripts/container-smoke-test.sh                                     # PR smoke test
dotnet ef migrations add <Name> --project Katalog-Backend/Katalog-Backend.csproj
```

`Tests/Integration/*` need SQL Server and the emulator running (`docker compose up -d sqlserver sqledge servicebus-emulator`).

## Structure

- `Katalog-Backend/` is one ASP.NET Core project (.NET 10, EF Core + SQL Server, Identity/JWT). It is layered as Controllers → Services → Repositories → `ApplicationDbContext`, with `Mappers/` holding the entity↔DTO extension methods.
- The **API** and the **consumer** are the same image. `BackgroundServices__ConsumerEnabled` and `BackgroundServices__OutboxPublisherEnabled` decide which role a process runs. Compose runs them as the `api` and `consumer` services.
- `Katalog-Backend.Tests/` holds NUnit tests: Moq unit tests for services, `WebApplicationFactory<Program>` tests for the API, and in-memory OTel exporter tests in `Observability/`.

## Purchase flow

1. `POST /api/orders` → `OrderService.CreateOrderAsync` validates stock. In a single transaction it saves a `Pending` order and an `OutboxMessage` (`OrderPlacedEvent`, SessionId = order id, `TraceParent`), then returns `202`.
2. `OutboxPublisherWorker` (consumer process) publishes outbox rows to the session-enabled `orders-queue` and propagates the trace context.
3. `OrderStockDecrementConsumer` decrements stock in a transaction and sets the order to `Confirmed`, or sets it to `Failed` (insufficient stock / variant missing). It skips orders that are no longer `Pending`, which makes redelivery idempotent.

Trace context must survive API → outbox → Service Bus → consumer (`TelemetryTests` asserts this). Metrics and the `ActivitySource` live in `Observability/Telemetry.cs`.

## Conventions

- Add endpoints as controllers, not minimal APIs.
- Use DTOs at the API boundary. Never return or accept EF entities.
- Throw domain exceptions from `Exceptions/` and register each one in `Middleware/GlobalExceptionHandler.MapException`, otherwise it becomes a 500.
- API-level tests use NUnit with `WebApplicationFactory<Program>`.
- Write logs as `ILogger` message templates (`LogWarning("Order {OrderId} rejected", id)`), never string interpolation.
- Keep personal data (emails, names, user IDs, tokens) out of logs and span attributes. Order, variant and message IDs are fine.
- Metric tags must be low-cardinality (outcome, reason, route). Never use IDs, users or free text.
- Never edit a migration that has already been applied. Add a new one. CI fails on pending model changes.
- PRs target `dev`.
- Restarting the `servicebus-emulator` container drops every queued message. For recovery tests, restart only `consumer`.
