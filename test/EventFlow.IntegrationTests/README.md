# Project API integration tests

Run from the repository root with the .NET 10 SDK:

```powershell
dotnet test test/EventFlow.IntegrationTests/EventFlow.IntegrationTests.csproj -m:1
```

`ProjectApiTests` exercises the real API through `WebApplicationFactory<Program>`:
HTTP routing, model binding, registration/login, Identity, JWT authentication,
authorization, error middleware, application services, and EF Core repositories.
The only infrastructure replacement is the database provider. No services or
authentication handlers are mocked.

Each test (including each theory case) owns a separate in-memory SQLite database
with foreign keys enabled. Requests use separate DI scopes; database assertions
reload records in fresh scopes. The database is discarded when the test ends.
No Docker, PostgreSQL instance, development database, or local secrets are needed.
Test JWT settings override application configuration.

The 57 cases cover:

- Authenticated creation, persisted owner membership, and the Location header.
- Owner/member retrieval, hidden projects for non-members, and list isolation.
- Updating names as owner/admin/developer; denying read-only and unrelated users.
- Deactivation/reactivation, role permissions, and repeated state transitions.
- Unauthenticated requests to every project endpoint.
- Malformed, expired, incorrectly signed, wrong-issuer, and wrong-audience JWTs,
  plus signed tokens with missing or invalid user IDs.
- Missing projects, malformed IDs/bodies, blank names, and spoofed ownership.
- Persisted results and unchanged data after rejected writes.

Memberships for shared-project scenarios are seeded with the domain invitation
method because invitations do not yet have an HTTP endpoint. All users are
registered and logged in through the real authentication endpoints.

SQLite validates these API flows and relational persistence, but this suite does
not validate PostgreSQL migrations, PostgreSQL-specific types (such as `jsonb`),
or provider-specific behavior such as enforcement of column length limits.
