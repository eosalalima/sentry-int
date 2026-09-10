# ADR 0001: Consolidated Sentry host

**Status:** Proposed pending source-schema reconciliation  
**Date:** 2026-09-10

## Context and decision

One ASP.NET Core Blazor Web App owns presentation, polling, gate insertion and SMS processing. `SentryIntegrated.csproj` is the sole production project and `Program.cs` is the sole entry point. Interactive server rendering provides immediate in-process UI updates: a singleton, lock-protected `DashboardState` raises change notifications to each circuit; components unsubscribe when disposed. State is bounded by feed and queue limits and events are keyed by the stable database ID.

The former SentryApp responsibility maps to dashboard components, `PersonnelResolver`, `PhotoResolver`, `TurnstilePollingWorker`, and `DashboardState`. SentryGate maps to a Blazor page, protected minimal API, validation service and demo hosted worker. SentrySMS maps to a bounded channel, template/normalization services, durable delivery ledger, retrying hosted worker, and provider-specific `ISmsSender` implementations.

Three pooled `IDbContextFactory` registrations isolate access-control, staff, and student lifetimes. Externally owned `DeviceLogs`/personnel tables are never migrated automatically. Two application-owned tables hold the durable polling watermark and SMS delivery ledger. Operations use async EF queries and cancellation. Staff wins a collision, followed by student, then a non-sensitive unknown fallback; this precedence is provisional until source access is restored.

## Configuration and security

All options live below `Sentry`; all database endpoints use named connection strings. Safe defaults select **Live** and **Disabled** SMS. Credentials belong in user secrets, environment variables (`ConnectionStrings__AccessControl`, etc.), or a production secret store. Gate HTTP mutation requires server authorization; Blazor form posts run over the antiforgery-protected circuit. Errors exposed to clients contain validation guidance rather than database details. Logs use event IDs/device IDs and never message bodies or complete phone numbers.

## Background responsibilities

The polling worker reads IDs greater than the persistent watermark in ID order, enriches each row, updates shared state, queues applicable SMS, and advances its watermark after each row. It applies bounded transient retry and observes shutdown cancellation. Demo mode inserts a regular log row through `IGateService`; it never publishes directly to the UI. SMS uses a bounded queue and durable per-event attempts. GSM selection without a port fails safely.

## Deployment

`dotnet publish` creates one host package. IIS uses the SDK-generated `web.config`; Kestrel may run the published DLL behind a TLS reverse proxy. No migrations run at startup, protecting operational vendor tables. A DBA must provision application-owned tables after validating the supplied mappings against the actual source schema.

## Known consequences

In-process dashboard history is intentionally rebuilt from newly polled events after a process restart, while the durable watermark prevents replay. Horizontal scale requires replacing the in-process distributor/worker lease with a distributed implementation. Source access was blocked, so schema and legacy-contract decisions remain provisional as listed in the traceability matrix.
