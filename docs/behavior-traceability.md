# Behavior traceability

## Analysis provenance

The source clone attempt on 2026-09-10 failed before any source bytes were received (`CONNECT tunnel failed, response 403`). The source branch and commit therefore cannot be independently recorded in this environment. The target started on branch `work` at `7ea73e9e4dfc4409d0189a549fca93bbc5c6f73a`; implementation moved to `feature/consolidate-sentry-services`. Rows marked **Uncertain** are deliberately not represented as source-confirmed facts. They must be reconciled against an owner-provided source checkout before production rollout.

| Source project | Source files | Existing behavior | Trigger/input | Expected output | Consolidated destination | Verification method | Status |
|---|---|---|---|---|---|---|---|
| SentryApp | unavailable | Server-hosted monitoring UI inferred from brief | Browser navigation | Responsive spotlight, queue and feed | `Components/Pages/Dashboard.razor`, `DashboardState` | component/browser smoke test | Implemented |
| SentryApp | unavailable | Polls `DeviceLogs` | New increasing stable row ID | Ordered enriched event, persisted watermark | `TurnstilePollingWorker` | SQL integration test | Implemented |
| SentryApp | unavailable | Avoid repeated events | Previously processed row | No repeated display | watermark plus `DashboardState` ID set | restart and unit tests | Implemented |
| SentryApp | unavailable | Staff then student personnel resolution | Access number | formatted person or unknown fallback | `PersonnelResolver` | EF integration tests | Implemented |
| SentryApp | unavailable | Personnel photo/fallback | photo filename | escaped URL or fallback SVG | `PhotoResolver` | `RulesTests` | Verified |
| SentryApp | unavailable | Configurable spotlight and feed | elapsed time | spotlight moves to bounded history; next queued item promoted | `DashboardState` | `RulesTests` | Verified |
| SentryApp | unavailable | Database error state | polling failure | retry and non-sensitive UI warning | polling worker/dashboard | failure integration/browser test | Implemented |
| SentryApp | unavailable | Exact table/column mappings | SQL Server schema | compatibility with operational schema | EF contexts | compare with source/schema | Uncertain |
| SentryGate | unavailable | Inserts a gate event in `DeviceLogs` | validated personnel/device/event input | durable row consumed by normal polling | `GateService`, `/gate`, `/api/gate/events` | EF/API integration test | Implemented |
| SentryGate | unavailable | Exact legacy HTTP routes/contracts and authentication | external HTTP request | compatible response | protected `/api/gate/events` | compare with source/client | Uncertain |
| SentryGate | unavailable | Demo uses genuine personnel/device | periodic demo trigger | inserted `DeviceLogs` record | `DemoEventWorker` | SQL integration test | Implemented |
| SentryGate | unavailable | Live mode creates no simulation | `Sentry:Mode=Live` | demo worker exits | `DemoEventWorker` | hosted service test | Implemented |
| SentrySMS | unavailable | Notification constructed for event/person | enriched event with phone | normalized recipient and templated message | `SmsTemplateService` | `RulesTests` | Verified |
| SentrySMS | unavailable | Serial GSM modem AT workflow | queued SMS | text mode/send sequence with bounded timeout | `GsmModemSmsSender` | fake serial abstraction still required | Implemented |
| SentrySMS | unavailable | Disabled/development behavior | provider configuration | safe failure or recording | sender implementations | unit test | Verified |
| SentrySMS | unavailable | Retry and duplicate prevention | send failure/replayed event | bounded retries; durable delivery ID | `SmsWorker`, `SentrySmsDeliveries` | EF/worker integration test | Implemented |
| SentrySMS | unavailable | Exact message, phone rules, AT acknowledgements | source-specific values | source-identical behavior | messaging module | source comparison/hardware acceptance | Uncertain |
| All | requested specification | One host/config/logging/deployment | process start | one Blazor application | `Program.cs`, production `.csproj` | build/publish/process inspection | Implemented |
| All | requested specification | Liveness/readiness | HTTP probe | safe health response | `/health/live`, `/health/ready` | endpoint test | Implemented |

## Required reconciliation

The operational table names, keys, nullability, data types, lookup precedence, formatting, exact event transition timing, legacy routes, modem acknowledgement parsing, and source authentication cannot truthfully be called confirmed until the source repository is available. No invented source detail is marked Confirmed.
