# Sentry Integrated

Sentry Integrated consolidates turnstile monitoring, gate/demo event generation, and SMS delivery into one interactive-server Blazor host. It has one production project, configuration root, DI container, logging pipeline, process, and publish package. The implementation target is .NET 10, the current LTS for the stated 2026 deployment date.

> **Source-analysis limitation:** the implementation environment could not clone `eosalalima/sentry` (`CONNECT tunnel failed, response 403`). Consequently, operational mappings and legacy contracts must be reconciled with a source checkout before production. See [behavior traceability](docs/behavior-traceability.md) and the [architecture decision](docs/architecture-decision-record.md).

## Component map

| Original responsibility | Integrated module |
|---|---|
| SentryApp monitor/polling/personnel/photos | `Components/Pages/Dashboard.razor`, `Application/Turnstile`, `Application/Personnel`, `Infrastructure/Polling` |
| SentryGate event entry/demo generation | `Components/Pages/Gate.razor`, `Application/Gate`, protected `POST /api/gate/events`, `DemoEventWorker` |
| SentrySMS templates/queue/GSM | `Application/Messaging` and `SmsWorker` |

The polling, demo, and SMS workers are hosted inside the web process. A gate/demo insertion always writes `DeviceLogs`; only the polling worker publishes it. The monitor maintains one thread-safe bounded state and interactive Blazor circuits receive updates without refreshing.

## Prerequisites and clean-checkout commands

* .NET 10 SDK and ASP.NET Core Runtime
* SQL Server reachable by the host
* Three least-privilege application identities or connection strings
* Optional Windows/Linux serial-device access for GSM

```bash
dotnet --info
dotnet restore SentryIntegrated.sln
dotnet build SentryIntegrated.sln --configuration Release --no-restore
dotnet test SentryIntegrated.sln --configuration Release --no-build
dotnet run --project SentryIntegrated/SentryIntegrated.csproj
```

## SQL Server and permissions

Named connections are `AccessControl`, `Staff`, and `Students`. Provision credentials outside Git:

```bash
dotnet user-secrets --project SentryIntegrated set "ConnectionStrings:AccessControl" "..."
dotnet user-secrets --project SentryIntegrated set "ConnectionStrings:Staff" "..."
dotnet user-secrets --project SentryIntegrated set "ConnectionStrings:Students" "..."
```

Environment variables use ASP.NET Core double underscores, for example `ConnectionStrings__AccessControl`. The access identity needs `SELECT` on `DeviceLogs` and `Devices`, `INSERT` on `DeviceLogs`, and `SELECT/INSERT/UPDATE` on application-owned `SentryProcessingWatermarks` and `SentrySmsDeliveries`. Staff/student identities need only `SELECT` on their mapped tables. Do **not** grant schema-owner privileges. This application does not migrate or recreate vendor tables. Confirm all mappings in `SentryDbContext.cs` with the source and production schema first.

## Configuration

| Key | Purpose/default |
|---|---|
| `Sentry:Mode` | `Live` (safe default) or `Demo` |
| `Sentry:Polling:IntervalMilliseconds`, `BatchSize`, `RetryCount` | Poll cadence, ordered batch size, bounded retries |
| `Sentry:Dashboard:SpotlightMilliseconds`, `FeedLimit`, `QueueLimit` | Central transition timing and memory bounds |
| `Sentry:Photos:BaseUrl`, `FallbackUrl` | Photo URL and safe fallback |
| `Sentry:Gate:MaximumAccessNumberLength`, `DemoIntervalSeconds` | Validation and demo cadence |
| `Sentry:Sms:Provider` | `Disabled`, `Recording`, or `GsmModem` |
| `Sentry:Sms:MaximumAttempts`, `RetryDelayMilliseconds`, `CountryCode` | Bounded delivery and normalization |
| `Sentry:Sms:Gsm:PortName`, `BaudRate`, `TimeoutSeconds` | Serial modem parameters |

`appsettings.Example.json` contains placeholders. Production should set `ASPNETCORE_ENVIRONMENT=Production`, keep `Mode=Live` unless explicitly demonstrating, and inject secrets through environment variables or an approved store.

### Demo and live modes

In `Demo`, the worker chooses an existing staff member (otherwise a student) and existing device, then inserts a normal gate record periodically. The polling/watermark/enrichment/dashboard/SMS path is identical to an external event. Empty personnel/device tables cause a warning and no fabricated identity. In `Live`, the demo worker exits and external equipment owns log generation.

### GSM modem

Set `Provider=GsmModem`, port (for example `COM3` or `/dev/ttyUSB0`), baud rate, and timeout. Grant the service account access to that device. The sender opens the port for each message and issues `AT`, text-mode `AT+CMGF=1`, `AT+CMGS`, then body plus Ctrl-Z. A missing/inaccessible port produces a bounded failure rather than preventing web startup. `Recording` stores only event ID/time in memory and is intended for development. No test suite requires hardware.

## Operations and health

* `GET /health/live` verifies the host is running.
* `GET /health/ready` verifies all databases and a recent successful polling cycle.
* `/` is the responsive monitor; `/gate` is in-host event entry.
* `POST /api/gate/events` is protected and validates a narrow request contract.

If readiness is unhealthy, verify the three connections, SQL permissions, mapped schema, and polling logs. If photos fail, verify the configured URL and web-server access; the UI substitutes its bundled silhouette. If SMS fails, verify provider/port permissions and use `Recording` to isolate hardware.

## Publish and deployment

Exact publish command:

```bash
dotnet publish ./SentryIntegrated/SentryIntegrated.csproj --configuration Release --output ./publish
```

For Kestrel, copy `publish/`, inject configuration, and run `dotnet SentryIntegrated.dll` under a restricted service account behind a TLS reverse proxy. For IIS, install the .NET Hosting Bundle, create an application pool with **No Managed Code**, deploy the folder, inject environment settings, and point IIS at the SDK-generated `web.config`. Only this one application/package is deployed.

Back up the application-owned watermark/delivery tables and current configuration before rollout. Keep the previous publish directory. Roll back by stopping traffic, restoring the prior package/config, and retaining the ledgers so events/SMS are not replayed. Database schema changes require a separately reviewed DBA script.

## Known differences and risks

Because source access was unavailable, exact source schema, authentication, UI styling, legacy routes, message wording, phone rules, event transition details, and modem response semantics are not verified. Current GSM writes the required sequence but does not parse modem acknowledgements. Dashboard history is process-local and only new post-watermark events appear after restart. One worker instance is assumed; multi-instance deployment needs a distributed lease/backplane. These are release blockers until reconciled and tested against a staging clone.
