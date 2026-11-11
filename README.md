# EventFlow

EventFlow is a webhook and event delivery platform for reliably publishing events to subscribed endpoints. It provides asynchronous delivery, signature verification, automatic retries, delivery observability, event replay, and dead-letter event management.

## Features

- Create and manage projects
- Generate and revoke API keys
- Register webhook endpoints
- Subscribe endpoints to event types
- Publish events to subscribed endpoints
- Deliver events asynchronously
- Automatically retry failed delivery attempts
- Verify webhook signatures
- Inspect delivery attempts and delivery health
- Replay events
- Manage dead-letter events
- Receive failure notifications

## Technology Stack

### Backend

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- ASP.NET Core Identity
- JWT authentication with refresh tokens
- Redis
- Hangfire
- Serilog
- FluentValidation
- xUnit and integration tests

### Frontend

- React
- TypeScript
- Vite
- React Router
- TanStack Query
- Zustand
- React Hook Form
- Zod
- Tailwind CSS

### Infrastructure

- Docker
- Docker Compose
- GitHub Actions
- PostgreSQL
- Redis

## Delivery Lifecycle

1. Create a project and generate an API key.
2. Register webhook endpoints and subscribe them to event types.
3. Publish an event through the API.
4. EventFlow queues deliveries for matching subscriptions.
5. Delivery attempts are signed and sent asynchronously.
6. Failed attempts are retried automatically.
7. Events that exhaust their retry policy are available in the dead-letter queue for inspection or replay.

## Status

EventFlow is under active development.

## Background delivery processing

The API host runs a database-backed delivery worker. Event publication saves the
event and its queued deliveries in the same transaction; the worker polls those
records directly, so no separate broker or enqueue operation is required.

Apply the migrations before starting the updated API:

```powershell
dotnet ef database update --project src/EventFlow.Infrastructure --startup-project src/EventFlow.Presentation.Api
dotnet run --project src/EventFlow.Presentation.Api
```

`DeliveryProcessing` in `appsettings.json` controls the worker:

| Setting | Default | Behavior |
| --- | --- | --- |
| `Enabled` | `true` | Set to `false` to accept events without running background sends in this host. |
| `WorkerCount` | `4` | Concurrent processing loops per API instance, each with its own database scope. |
| `PollInterval` | `00:00:02` | Wait after an empty queue or an infrastructure error. |
| `LeaseDuration` | `00:01:00` | Interrupted work becomes claimable after this interval. Must exceed `WebhookSender:Timeout` by more than ten seconds. |
| `MaxAttempts` | `5` | Total completed attempts, including the first send. |
| `InitialRetryDelay` | `00:00:30` | Initial exponential-backoff ceiling; jitter selects between half and the full ceiling. |
| `MaxRetryDelay` | `01:00:00` | Maximum delay, including a receiver's `Retry-After`. |

Workers atomically claim queued deliveries, due retries, or expired processing
leases. They skip inactive projects and endpoints; reactivation resumes eligible
work. Each attempt decrypts the endpoint secret, sends the stored payload through
the signed webhook sender, and saves status, response code, bounded response body,
error, duration, and attempt time. Retryable failures use exponential backoff;
permanent failures and exhausted retries move to `DeadLetter`.

Completion is fenced by the lease token and commits attempt history with the
delivery state. An outdated worker cannot overwrite a replacement worker's result.
Delivery is **at least once**: a crash after sending but before committing can send
the same delivery again. Receivers should deduplicate `EventFlow-Delivery-Id`.
Graceful shutdown cancels active requests; unfinished leases expire for recovery.
Multiple hosts must use synchronized clocks and share access to the same signing
key protection material. The current DPAPI configuration remains Windows-specific.

Delivery inspection/replay HTTP APIs and UI are still planned. Delivery and attempt
records can currently be inspected in the database.

Run the delivery-focused tests:

```powershell
dotnet test EventFlow.slnx --filter "FullyQualifiedName~DeliveryProcessorTests|FullyQualifiedName~DeliveryWorkerTests|FullyQualifiedName~DeliveryProcessingTests"
```


dotnet ef migrations add InitialCreate --project EventFlow.Infrastructure --startup-project EventFlow.Presentation.Api --output-dir EventFlow.Infrastructure/Persistence/Migrations


dotnet ef database update --project src/EventFlow.Infrastructure --startup-project src/EventFlow.Presentation.Api
