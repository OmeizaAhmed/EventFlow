# EventFlow

EventFlow sends signed webhooks to subscribed endpoints. Projects can publish
events with API keys, inspect delivery attempts, and replay deliveries after
failures.

## Current status

The API supports browser sessions, project management, endpoint subscriptions,
API key management, event history, delivery inspection and replay, and project
invitations. Hangfire runs webhook delivery and notification jobs. SMTP email
can notify project owners of dead-lettered deliveries and send invitations to
registered users.

The React client has a welcome page and an invitation sign-in and acceptance
screen. The next milestone is the delivery dashboard, from project selection
through delivery filtering, attempt inspection, and replay.

## Technology stack

- **Backend** uses .NET 10, ASP.NET Core, EF Core, PostgreSQL, ASP.NET Core Identity
  and JWT access tokens with rotating refresh cookies.
- **Background processing** uses Hangfire with PostgreSQL storage. MailKit sends
  SMTP email, and Serilog provides structured logging.
- **Client** uses React, TypeScript, Vite and Tailwind CSS.
- **Tests** use xUnit with SQLite by default and optional PostgreSQL delivery tests.

## Run locally

Use Windows, the .NET 10 SDK, the `dotnet-ef` 10.x tool, and a local PostgreSQL
database. The API uses Windows DPAPI to protect its data protection keys. The
React client also requires Node.js 20.19+ on the 20.x line, or Node.js 22.12+,
and npm.

From the repository root, point the API at your development database and
configure JWT signing. These settings apply to the current PowerShell session.
Use .NET user secrets if you want the settings to persist between sessions.

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Database=eventflow_dev;Username=YOUR_USER;Password=YOUR_PASSWORD'
$env:JwtSettings__Issuer = 'EventFlow.Local'
$env:JwtSettings__Audience = 'EventFlow.Local'
$env:JwtSettings__SecretKey = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')

dotnet dev-certs https --trust
dotnet ef database update --project src/EventFlow.Infrastructure --startup-project src/EventFlow.Presentation.Api -- --environment Development
dotnet run --project src/EventFlow.Presentation.Api --launch-profile https
```

The update applies all pending migrations, including `NotificationOutbox`,
`AddProjectInvitations`, and `AddRefreshTokenSessions`. The API initializes the
`hangfire` schema during startup when a Hangfire server is enabled. The database role needs permission
to apply the EF migrations and create or upgrade the Hangfire schema.

The API listens on `https://localhost:7056`. Open the
[Scalar API reference](https://localhost:7056/scalar/) to explore the routes.
Start the React client in a second terminal from the repository root.

```powershell
Set-Location eventFlow.client
npm ci
npm run dev
```

The client listens on `http://localhost:5173` and proxies `/api` to the API's
HTTPS launch profile. Email delivery is disabled by default. See
[Notifications](#notifications) to configure SMTP and the invitation link.

## Delivery lifecycle

1. Create a project and generate an API key.
2. Register webhook endpoints and subscribe them to event types.
3. Publish with `POST /api/projects/{projectId}/events`, a live API key in the
   `Authorization: Bearer` or `X-API-Key` header, and an `Idempotency-Key` header.
4. The API returns `202 Accepted`. EventFlow stores the event and queues
   deliveries for matching subscriptions.
5. Delivery attempts are signed and sent asynchronously.
6. Temporary failures are retried automatically within the retry policy.
7. Successful deliveries become `Delivered`. Permanent failures and exhausted
   retries move to `DeadLetter` for inspection and replay.

## Dashboard API

- `POST /api/auth/register` creates an account.
  `POST /api/auth/login` and `POST /api/auth/refresh` return an access token
  and set the refresh cookie. `GET /api/auth/me` uses the access token to return
  the current user's ID, email, names and roles. `POST /api/auth/logout` revokes
  that browser's refresh session and clears the cookie. Issued access tokens
  remain valid until they expire.
- `GET /api/projects` and `GET /api/projects/{projectId}` include `isActive`
  and the current member's role.
- `GET /api/projects/{projectId}/events` lists events newest first for active
  project members. It accepts `page`, `pageSize` and an exact `eventType` filter.
  List items include a delivery count. `GET /api/projects/{projectId}/events/{eventId}`
  also returns the stored payload as a JSON string.
- API key details and rotation stay within the requested project. A successful
  rotation commits the replacement and scheduled revocation together. The
  replacement secret is returned once, and the old key has a one-hour grace period.

## Logging

The API uses Serilog for console output. Configuration lives under `Serilog` in
`appsettings.json`; log levels can be overridden per environment. Requests log
method, path, status and elapsed time. Application services log state changes
using IDs, while the delivery worker logs attempts, retries and failures.
Credentials, signing secrets, refresh tokens and event payloads are not logged.

## Background delivery processing

The API host runs a Hangfire server backed by PostgreSQL. Event publication saves
the event and its queued deliveries in the same transaction. After a delivery or
replay is committed, EventFlow enqueues a Hangfire job for its delivery ID.
Hangfire schedules retry jobs after EventFlow saves the next attempt time. A
recurring job scans for due deliveries and expired leases every minute, up to
100 deliveries per run, so work also resumes after a missed enqueue or restart.

`DeliveryProcessing` in `appsettings.json` controls the worker:

| Setting | Default | Behavior |
| --- | --- | --- |
| `Enabled` | `true` | Set to `false` to accept events without running background sends in this host. |
| `WorkerCount` | `4` | Concurrent Hangfire workers per API instance. |
| `PollInterval` | `00:00:02` | Hangfire queue polling interval. Checks for scheduled jobs use this interval, capped at one minute. |
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
Use `GET /api/projects/{projectId}/deliveries?status=DeadLetter&pageSize=25` to find
dead-lettered deliveries.
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

See the [webhook delivery test guide](DELIVERY_TEST_GUIDE.md) for the automated
suite and a manual walkthrough with a local HTTPS receiver.

## Project memberships

Membership routes use a user JWT and are scoped to `/api/projects/{projectId}/members`.
Active members can view the roster. Owners and admins can manage members, and
only the owner can appoint admins or manage an admin membership. Deactivation
removes project access while preserving the membership and its audit fields.

| Method | Path relative to `/members` | Operation |
| --- | --- | --- |
| GET | `/` | List memberships and roles. |
| GET | `/{membershipId}` | View a membership. |
| POST | `/` | Create a pending invitation by registered `email` or `inviteeId`, with an optional `role` that defaults to `ReadOnly`. |
| PUT | `/{membershipId}/role` | Set `role` to `Admin`, `Developer`, or `ReadOnly`. |
| POST | `/{membershipId}/deactivate` | Deactivate a membership. |
| POST | `/{membershipId}/activate` | Reactivate a membership. |
| POST | `/{membershipId}/transfer-ownership` | Transfer ownership to an active admin and make the former owner an admin. |

### Invitation and acceptance

An active owner or admin can invite a registered EventFlow account. Only an
owner can invite an admin. The invitation expires after seven days, and the
project appears in the invitee's project list after they sign in and accept.
Acceptance checks the inviter's current permissions and commits the membership
and invitation status together. Repeating acceptance returns the existing
membership. Existing memberships keep their current access.

| Method | Route | Operation |
| --- | --- | --- |
| GET | `/api/project-invitations/{invitationId}` | View details as the invitee or a project manager. |
| POST | `/api/project-invitations/{invitationId}/accept` | Accept as the invited account. |
| GET | `/api/projects/{projectId}/invitations` | List pending invitations as an owner or admin. |
| DELETE | `/api/projects/{projectId}/invitations/{invitationId}` | Revoke a pending invitation. Admin invitations require the owner. |

Invitation emails link to the client at `/invitations/accept`. Development uses
`http://localhost:5173`. In a deployed environment, set
`ProjectInvitations__PublicAppUrl` to the HTTPS client origin and serve the React
app and `/api` from that origin.

## Notifications

A terminal webhook failure writes a `DeliveryFailedNotification` to the outbox
in the same transaction that records the `DeadLetter` status. A subscriber sends
a plain-text email to the active project owner at dispatch time. The email
contains identifiers, attempt count and last HTTP status. Webhook payloads,
receiver responses and signing secrets stay out of the email.

Creating a pending invitation writes a `ProjectInvitationCreated` notification
in the same transaction. Its subscriber emails the invitee a link to the
confirmation screen. The invited account must still sign in and accept.

Other features can inject `INotificationPublisher` and call
`PublishAsync(new YourNotification(...), cancellationToken)`. Implement
`INotification` with a stable, versioned name and register each
`INotificationHandler<YourNotification>` with
`services.AddNotificationHandler<YourNotification, YourHandler>()`.
Hangfire scans the outbox once a minute. Subscribers should be safe to run
again if a handler fails or the host stops during dispatch. After five failed
attempts, a notification remains in `NotificationOutbox` with `FailedAt` set.

Notification dispatch is disabled by default, and queued notifications are
retained. To send email, set
`Notifications__Enabled=true` and configure the SMTP settings below.
Store SMTP credentials in environment secrets or .NET user secrets.

| Environment variable | Purpose |
| --- | --- |
| `Notifications__Smtp__Host` | SMTP server hostname. |
| `Notifications__Smtp__Port` | SMTP port, defaulting to `587`. |
| `Notifications__Smtp__ImplicitTls` | Set to `true` for implicit TLS, typically on port `465`. By default EventFlow requires STARTTLS. |
| `Notifications__Smtp__FromAddress` | Sender mailbox accepted by the SMTP server. |
| `Notifications__Smtp__Username`, `Notifications__Smtp__Password` | Supply both for authenticated SMTP. |

## Tests

Run the backend suite from the repository root.

```powershell
dotnet test EventFlow.slnx
```

The integration tests use isolated SQLite databases by default. The
[delivery test guide](DELIVERY_TEST_GUIDE.md) covers PostgreSQL-backed delivery
tests and a manual end-to-end run with a local HTTPS receiver.

Run the frontend checks from `eventFlow.client`.

```powershell
npm run lint
npm run build
```
