# Katalog-Backend

An e-commerce backend built with **ASP.NET Core (.NET 10)**, **Entity Framework Core**, **SQL Server**, and **Azure Service Bus**.

---

## Architecture & Services

The application is containerized and managed via Docker Compose (`compose.yaml`):

| Service | Container Name | Description | Ports |
| :--- | :--- | :--- | :--- |
| **`sqlserver`** | `katalog-sqlserver` | Microsoft SQL Server 2022 database with a **named volume** (`sqlserver_data`) ensuring persistent data across restarts. | `1433:1433` |
| **`sqledge`** | `katalog-sqledge` | Azure SQL Edge container providing lightweight database backend for the Service Bus emulator. | Internal |
| **`servicebus-emulator`**| `katalog-servicebus-emulator` | Local Azure Service Bus emulator using `mcr.microsoft.com/azure-messaging/servicebus-emulator:latest`. Pre-configured via `emulator/Config.json`. | `5672:5672` (AMQP)<br>`5300:5300` (Management) |
| **`api`** | `katalog-api` | ASP.NET Core Web API running as a **non-root user** (`app`, UID 1654). Handles HTTP requests and health checks. Background consumers are disabled (`Consumer__Enabled=false`). | `5297:8080` |
| **`consumer`** | `katalog-consumer` | Dedicated background worker process consuming `order-placed-topic` events and processing transactional outbox messages. Scalable independently of the API. | Internal |

---

## Getting Started

### 1. Configure Environment Variables
Copy `.env.example` to `.env`:
```bash
cp .env.example .env
```
*(Note: Never commit `.env` containing sensitive credentials to source control. It is explicitly listed in `.gitignore`.)*

### 2. Start Services with Docker Compose
Start all services in detached mode:
```bash
docker compose up -d
```

To view logs:
```bash
docker compose logs -f api
docker compose logs -f consumer
```

---

## Health Check Endpoints

The API exposes ASP.NET Core health check endpoints:

* **Liveness Probe**: `GET http://localhost:5297/health/live`
  * Verifies the process is alive and responsive.
  * Returns: `200 OK` (`{"status": "Healthy"}`)

* **Readiness Probe**: `GET http://localhost:5297/health/ready`
  * Verifies connectivity to the database (`ApplicationDbContext.CanConnectAsync()`) and Azure Service Bus topic.
  * Returns: `200 OK` when dependencies are reachable or `503 Service Unavailable` on failure.

---

## Stage 3: Consumer Recovery Testing

The API and Consumer are decoupled into separate processes. This enables testing **Consumer Recovery** (fault tolerance when the message consumer is temporarily unavailable):

1. **Simulate Consumer Downtime**:
   Stop **only** the consumer container:
   ```bash
   docker compose stop consumer
   ```
2. **Publish Orders via the API**:
   Submit orders to the API. Because the database and Service Bus emulator remain active, events are placed onto the Service Bus topic/queue without loss.
3. **Resume Consumer**:
   Start the consumer container back up:
   ```bash
   docker compose start consumer
   ```
   The consumer re-connects and drains the backlog of messages.

> ⚠️ **CRITICAL NOTE ON EMULATOR LIFECYCLE**:  
> The Azure Service Bus emulator retains message state in memory/volatile container storage and **loses all queued messages if its container is restarted**.  
> **During Consumer Recovery tests, stop or restart ONLY the `consumer` container (`docker compose stop consumer`), NEVER the `servicebus-emulator` container.**

---

## Azure Service Bus Emulator: Known Limitations

When developing locally against `mcr.microsoft.com/azure-messaging/servicebus-emulator`, be aware of the following known emulator limitations:

1. **Volatile Message Storage Across Restarts**:
   The emulator does not persist message payloads across container restarts. Any messages published prior to restarting the emulator container are lost.
2. **Pre-defined Entity Configuration**:
   All namespaces, queues (including session requirements), topics, and subscriptions must be declared upfront in `emulator/Config.json`. Dynamically creating entities at runtime via the SDK is not fully supported without the dedicated management HTTP endpoint.
3. **Separate Management HTTP Port**:
   Management operations (`ServiceBusAdministrationClient`) communicate over HTTP port `5300`, whereas standard message publishing and consuming (`ServiceBusClient`) use AMQP on port `5672`. The connection string format for local administration differs from standard AMQP messaging.
4. **No Cross-Entity Transactions**:
   Transactions spanning multiple entities (e.g. sending to topic A and acknowledging from queue B in a single transaction) are not supported by the emulator.
5. **Relaxed Development Authentication**:
   The emulator operates with `UseDevelopmentEmulator=true;SharedAccessKey=SAS_KEY_VALUE;` and does not support Azure Entra ID (Azure AD), OAuth tokens, or role-based access control (RBAC).
6. **No Premium Tier Features**:
   Features such as geo-disaster recovery, partitioning, customer-managed keys (CMK), and message migration across namespaces are unavailable in the local emulator.
