# Testing webhook delivery in EventFlow

Run the automated suite for a quick check, or follow the manual walkthrough to
watch the API send real HTTPS requests to a local receiver.

## Run the automated tests

From the EventFlow repository root, run this command with the .NET 10 SDK.

```powershell
dotnet test EventFlow.slnx --filter "FullyQualifiedName~DeliveryProcessorTests|FullyQualifiedName~DeliveryProcessingTests|FullyQualifiedName~ProjectApiTests.Deliveries_"
```

The suite covers signed sends, retries, dead-lettering, lease recovery,
delivery inspection, and replay. The integration tests use SQLite by default.
To run the delivery processing tests against PostgreSQL, set
`EVENTFLOW_DELIVERY_TEST_POSTGRES` to a connection string for a PostgreSQL
test account that can create and drop temporary databases, then rerun the command.

## Start EventFlow and the receiver

### Terminal 1 — EventFlow API

Use a local PostgreSQL development database and make sure the API has
`ConnectionStrings:DefaultConnection` and `JwtSettings` configured. If you need
to configure them for this run, set the following variables in this terminal.
Replace the connection string with your development database connection string.

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Database=eventflow_dev;Username=postgres;Password=YOUR_LOCAL_PASSWORD'
$env:JwtSettings__Issuer = 'EventFlow.Local'
$env:JwtSettings__Audience = 'EventFlow.Local'
$env:JwtSettings__SecretKey = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
```

From the repository root, trust the local HTTPS certificate, apply the migrations,
and start the API with the HTTPS launch profile. The migration command requires
`dotnet ef`.

```powershell
dotnet dev-certs https --trust
dotnet ef database update --project src/EventFlow.Infrastructure --startup-project src/EventFlow.Presentation.Api
$env:DeliveryProcessing__Enabled = 'true'
dotnet run --project src/EventFlow.Presentation.Api --launch-profile https
```

Leave the API running at `https://localhost:7056`.

### Terminal 2 — HTTPS receiver

From a scratch directory outside the EventFlow repository, create a receiver
project.

```powershell
dotnet new web -o EventFlow.TestReceiver
Set-Location EventFlow.TestReceiver
```

Replace its `Program.cs` with the following code. Use a receiver like this
with test payloads because it logs the request body.

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var statusFile = Path.Combine(app.Environment.ContentRootPath, "status.txt");

app.MapPost("/webhooks", async (HttpRequest request, CancellationToken ct) =>
{
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync(ct);
    app.Logger.LogInformation("Delivery {DeliveryId} for {EventType}: {Payload}",
        request.Headers["EventFlow-Delivery-Id"].ToString(),
        request.Headers["EventFlow-Event-Type"].ToString(), payload);

    var status = File.Exists(statusFile)
        ? (await File.ReadAllTextAsync(statusFile, ct)).Trim()
        : "200";

    return Results.StatusCode(status switch
    {
        "400" => 400,
        "503" => 503,
        _ => 200
    });
});

app.Run();
```

Start the receiver and leave it running. This walkthrough assumes both apps
run on the same computer outside containers.

```powershell
dotnet run --no-launch-profile -- --urls https://localhost:7443
```

## Create a test project and subscribe the receiver

Open a third PowerShell terminal in the **receiver project directory**. A
fresh account and project keep the delivery list isolated for this run.

```powershell
$api = 'https://localhost:7056/api'
$receiverStatus = Join-Path $PWD 'status.txt'
$email = 'delivery-' + [guid]::NewGuid().ToString('N') + '@example.com'
$password = 'Local-test-9!' + [guid]::NewGuid().ToString('N')

function Post-Json($uri, $body, $headers = @{}) {
    Invoke-RestMethod -Method Post -Uri $uri -Headers $headers `
        -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 10)
}

Post-Json "$api/auth/register" @{
    email = $email
    password = $password
    firstName = 'Delivery'
    lastName = 'Tester'
}
$login = Post-Json "$api/auth/login" @{ email = $email; password = $password }
$userHeaders = @{ Authorization = "Bearer $($login.token)" }
$project = Post-Json "$api/projects" @{ name = 'Delivery test' } $userHeaders
$projectUrl = "$api/projects/$($project.projectId)"
$apiKey = Post-Json "$projectUrl/apikeys" @{ isLive = $true } $userHeaders
$endpoint = Post-Json "$projectUrl/endpoints" @{
    url = 'https://localhost:7443/webhooks'
    description = 'Local delivery receiver'
} $userHeaders
Post-Json "$projectUrl/endpoints/$($endpoint.endpointId)/subscriptions" @{
    eventType = 'order.created'
} $userHeaders
```

In the same terminal, add helpers to publish a fresh event, poll the latest
delivery for the endpoint, and inspect attempts. Publishing uses the live API
key. Inspection and replay use the user JWT.

```powershell
function Publish-TestEvent($orderId) {
    Post-Json "$projectUrl/events" @{
        eventType = 'order.created'
        payload = @{ orderId = $orderId }
    } @{
        'X-API-Key' = $apiKey.plainTextKey
        'Idempotency-Key' = [guid]::NewGuid().ToString()
    }
}

function Wait-DeliveryStatus($status, $deliveryId, $timeoutSeconds = 60) {
    $deadline = (Get-Date).AddSeconds($timeoutSeconds)
    do {
        if ($deliveryId) {
            $detail = Invoke-RestMethod "$projectUrl/deliveries/$deliveryId" -Headers $userHeaders
            $delivery = $detail.delivery
        } else {
            $listUrl = "$projectUrl/deliveries?endpointId=$($endpoint.endpointId)&pageSize=1"
            $page = Invoke-RestMethod $listUrl -Headers $userHeaders
            $delivery = $page.items | Select-Object -First 1
        }
        if ($delivery.status -eq $status) { return $delivery }
        Start-Sleep -Seconds 1
    } while ((Get-Date) -lt $deadline)

    throw "Expected $status. Last status: $($delivery.status) on $($delivery.deliveryId)."
}

function Show-Attempts($deliveryId) {
    $page = Invoke-RestMethod "$projectUrl/deliveries/$deliveryId/attempts" -Headers $userHeaders
    $page.items | Format-Table attemptNumber, httpStatusCode, succeeded, errorMessage
}
```

## Verify success, retries, and replay

Run one block at a time in terminal 3. `Publish-TestEvent` returns no response
body because the event API responds with `202 Accepted` and an empty body.

### Successful delivery

```powershell
Set-Content $receiverStatus 200
Publish-TestEvent 'success-001'
$delivered = Wait-DeliveryStatus 'Delivered'
Show-Attempts $delivered.deliveryId
```

Expect one attempt with HTTP `200` and `succeeded` set to `True`. The receiver
log should show the delivery ID and the `success-001` payload.

### Temporary failure and retry

Run the whole block so the receiver changes to `200` as soon as the first
failure is recorded. The first retry is due in roughly 15–30 seconds with the
default settings.

```powershell
Set-Content $receiverStatus 503
Publish-TestEvent 'retry-001'
$retry = Wait-DeliveryStatus 'RetryScheduled'
$retry | Select-Object deliveryId, attemptCount, nextRetryAt
Set-Content $receiverStatus 200
$recovered = Wait-DeliveryStatus 'Delivered' $retry.deliveryId
Show-Attempts $recovered.deliveryId
```

Expect HTTP `503` followed by `200` under the same delivery ID.

### Permanent failure and replay

```powershell
Set-Content $receiverStatus 400
Publish-TestEvent 'dead-letter-001'
$dead = Wait-DeliveryStatus 'DeadLetter'
Show-Attempts $dead.deliveryId

Set-Content $receiverStatus 200
$replay = Invoke-RestMethod -Method Post `
    -Uri "$projectUrl/deliveries/$($dead.deliveryId)/replay" -Headers $userHeaders
$replayed = Wait-DeliveryStatus 'Delivered' $replay.deliveryId
$original = Wait-DeliveryStatus 'DeadLetter' $dead.deliveryId
$original, $replayed | Format-Table deliveryId, eventId, status, replayOfDeliveryId, attemptCount
Show-Attempts $replayed.deliveryId
```

Expect the original delivery to stay `DeadLetter` with one HTTP `400` attempt.
The replay should reach `Delivered` with a new delivery ID, the same event ID,
and `replayOfDeliveryId` pointing to the original.

### Exhausted retries

Leave the receiver on `503` throughout this test. Allow up to nine minutes with
the default retry settings and a healthy local worker.

```powershell
Set-Content $receiverStatus 503
Publish-TestEvent 'exhausted-001'
$exhausted = Wait-DeliveryStatus 'DeadLetter' -timeoutSeconds 540
Show-Attempts $exhausted.deliveryId
Set-Content $receiverStatus 200
```

Expect five attempts returning `503`, followed by `DeadLetter`.

## Troubleshoot a missing delivery

| What you see | What to check |
| --- | --- |
| Publish returns `401` or `403` | Check that the API key is live and belongs to the project in the URL. |
| Publish succeeds and the list stays empty | Check that the endpoint is active, the `order.created` subscription exists, and you are filtering by the correct endpoint. |
| The delivery stays `Queued` | Check that the API started the Hangfire server and both the project and endpoint are active. |
| Attempts show a network or TLS error | Check that the receiver is listening at `https://localhost:7443` and the API process trusts the development certificate. |
