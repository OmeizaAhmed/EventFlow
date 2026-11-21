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

## Logging

The API uses Serilog for console output. Configuration lives under `Serilog` in
`appsettings.json`; log levels can be overridden per environment. Requests log
method, path, status and elapsed time. Application services log state changes
using IDs, while the delivery worker logs attempts, retries and failures.
Credentials, signing secrets, refresh tokens and event payloads are not logged.

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

### Delivery inspection and replay API

These endpoints require a user JWT and an active project membership. API keys
are for event publishing and cannot inspect or replay deliveries.

| Method | Route (under `/api/projects/{projectId}`) | Result |
| --- | --- | --- |
| GET | `/deliveries` | Paginated delivery summaries, newest first. |
| GET | `/deliveries/{deliveryId}` | Summary, original event payload, and current endpoint URL. |
| GET | `/deliveries/{deliveryId}/attempts` | Paginated attempt history in attempt-number order, including response, error, status code, and duration. |
| POST | `/deliveries/{deliveryId}/replay` | `202 Accepted`, new queued delivery summary, and a `Location` pointing to its details. |

Lists accept `status` (e.g. `DeadLetter`), `eventId`, and `endpointId` filters.
Both list routes accept `page` (default 1, maximum 1,000,000) and `pageSize`
(default 25, maximum 100), returning `items`, `totalCount`, `page`, and `pageSize`.
For example: `GET /api/projects/{projectId}/deliveries?status=DeadLetter&pageSize=25`.
Invalid filters or pagination return `400`. Missing deliveries and inaccessible
projects return `404`; read-only members attempting replay receive `403`.

All active members can inspect. Owners, admins, and developers can replay
`Delivered`, `Failed`, or `DeadLetter` deliveries when the project and endpoint
are active. Queued, processing, and retry-scheduled deliveries return `409` to
avoid duplicating work already in flight. Inactive destinations also return `409`.

Replay preserves the original delivery and attempts, creates a new delivery with
`replayOfDeliveryId` pointing to the source, and starts a fresh retry budget. It
reuses the stored event/payload and the same endpoint, using that endpoint's current
URL and signing key when sent. It does not re-evaluate event subscriptions. Each
successful replay POST creates a new delivery; repeated POSTs are not idempotent.
Receivers see the same event ID but a new delivery ID. The normal background worker
handles replays asynchronously, including retries and dead-lettering.

Apply the `DeliveryReplay` migration using the database update command above
before running this version. The inspection/replay UI remains planned.

Run the delivery-focused tests:

```powershell
dotnet test EventFlow.slnx --filter "FullyQualifiedName~DeliveryProcessorTests|FullyQualifiedName~DeliveryWorkerTests|FullyQualifiedName~DeliveryProcessingTests"
```


dotnet ef migrations add InitialCreate --project EventFlow.Infrastructure --startup-project EventFlow.Presentation.Api --output-dir EventFlow.Infrastructure/Persistence/Migrations


dotnet ef database update --project src/EventFlow.Infrastructure --startup-project src/EventFlow.Presentation.Api
