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


dotnet ef migrations add InitialCreate --project EventFlow.Infrastructure --startup-project EventFlow.Presentation.Api --output-dir EventFlow.Infrastructure/Persistence/Migrations


dotnet ef database update --project src/EventFlow.Infrastructure --startup-project src/EventFlow.Presentation.Api