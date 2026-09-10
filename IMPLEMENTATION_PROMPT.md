# Repository consolidation and reimplementation prompt

Use the following prompt with a coding agent that can read GitHub repositories, edit files, run commands, and create commits and pull requests.

---

## Prompt

You are a senior .NET architect and implementation agent. Reimplement the behavior of the source repository in the target repository while consolidating all production functionality into **one .NET Blazor application project**.

### Repositories

- **Source (read-only reference):** <https://github.com/eosalalima/sentry>
- **Target (write destination):** <https://github.com/eosalalima/sentry-int>

### Authorization and access permission

You have the repository owner's permission to:

1. Access and read every branch, file, commit, submodule, release, issue, and pull request in the source repository that is available to the supplied credentials.
2. Clone and inspect both repositories and use network access for GitHub and official package feeds.
3. Create, modify, move, and delete files in the target repository; restore dependencies; run builds, tests, migrations, and local services; and commit and push the completed work to a feature branch in the target repository.
4. Open a pull request in the target repository.
5. Reuse source code and assets only to the extent allowed by the source repository's license and the repository owner. Preserve required copyright and license notices. Do not copy secrets, credentials, personal data, generated build output, or environment-specific configuration.

If credentials cannot access either repository or a required dependency, report the exact blocked resource and continue with all work that does not require it. Never invent source behavior that you could have inspected with available access.

In the reimplemented application, preserve all user roles, authorization policies, permission checks, tenant or account boundaries, and denial behavior found in the source. Enforce authorization on the server at every protected page and endpoint; hiding UI elements is not sufficient. Default to least privilege and deny access when a rule is ambiguous.

### Objective

The source solution contains separate services/projects, including `SentryApp`, `SentryGate`, and `SentrySMS`. Study their actual implementation and reproduce their externally observable behavior in the target repository as a single deployable Blazor application. The result must have one production `.csproj`, one host process, one configuration model, and one deployment unit. Do not merely place the old projects in one solution, proxy to the old services, or retain multiple executables.

Use the latest supported .NET LTS SDK that is compatible with the target environment. Prefer Blazor Web App with the server-side interactive render mode unless source behavior demonstrates that another Blazor hosting model is required. Do not change externally visible behavior solely to fit that preference.

### Required workflow

#### 1. Discover instructions and establish a baseline

- Read all applicable `AGENTS.md`, `README`, contribution, license, CI, deployment, and configuration files in both repositories before editing.
- Record the current branches and commit SHAs of both repositories in the implementation notes so the comparison is reproducible.
- Build and test the source exactly as documented when feasible. Record commands and results, including environmental limitations.
- Build and test the untouched target before making changes.
- Do not commit directly to the default branch. Create a focused feature branch.

#### 2. Inventory source behavior before designing

Inspect the code rather than inferring behavior from project names. Produce a traceability matrix covering every capability in `SentryApp`, `SentryGate`, `SentrySMS`, shared libraries, scripts, and infrastructure. At minimum, inventory:

- routes, screens, layouts, forms, validation, navigation, loading/empty/error states, and responsive behavior;
- HTTP endpoints, request/response schemas, status codes, headers, authentication requirements, and error contracts;
- domain entities, relationships, invariants, workflows, state transitions, and business rules;
- persistence providers, schema, migrations, seed data, transactions, concurrency, retention, and data-protection behavior;
- login/logout, identity providers, cookies/tokens, roles, claims, policies, resource ownership, and administrative permissions;
- SMS providers, templates, inbound/outbound flows, delivery status, retries, deduplication/idempotency, callbacks/webhooks, rate limits, and failure handling;
- background jobs, timers, queues, notifications, real-time updates, integrations, and health checks;
- configuration keys, environment variables, secrets, logging, metrics, tracing, and deployment assumptions;
- accessibility, localization, time-zone, date/time, and formatting behavior;
- tests and fixtures that reveal behavior not documented elsewhere.

For each item, identify its source file(s), the consolidated destination component, and an objective verification method. Mark behavior as **confirmed**, **uncertain**, or **obsolete**, with evidence. Resolve uncertain behavior from tests, history, issues, or execution before implementation whenever possible.

#### 3. Design one application, not three co-located services

Create a short architecture decision record before substantial implementation. Map the old service boundaries into internal modules of the single Blazor project, for example:

- UI in `Components/` with pages and reusable components;
- application use cases behind explicit interfaces;
- domain models and rules independent of rendering and infrastructure;
- persistence and external integrations in infrastructure namespaces;
- former `SentryGate` HTTP behavior exposed by endpoints in the same ASP.NET Core host;
- former `SentrySMS` processing implemented as scoped services and, only when required, in-process `BackgroundService` workers;
- strongly typed options with startup validation for external providers;
- shared authentication and authorization policies applied consistently to pages and endpoints.

Keep one production `.csproj`. A solution file, documentation, scripts, and deployment assets are allowed, but do not create additional production applications or class-library projects. Do not require the source services at runtime. Preserve public contracts when existing clients may depend on them; document any unavoidable incompatibility and provide a migration path.

Avoid a mechanical file-for-file port. Remove duplicate DTOs and logic only after proving that consolidation preserves behavior. Use dependency injection and abstractions around time, persistence, SMS, and other external systems so important paths remain deterministic and testable.

#### 4. Implement in vertical slices

Implement one end-to-end capability at a time, including UI/API, domain rules, persistence, authorization, validation, logging, and tests. After each slice:

1. build the entire application;
2. run relevant automated tests;
3. compare it with the corresponding source behavior;
4. update the traceability matrix.

Preserve routes and API contracts where feasible. Use safe database migrations and never silently destroy existing target data. Use placeholders and documented environment variables for secrets. Provide local-development substitutes for external services (for example, an SMS recording provider), but ensure production providers are selected explicitly and fail safely when misconfigured.

For inbound webhooks, verify provider signatures when supported, reject unauthorized requests, avoid logging sensitive payload fields, and make repeated delivery idempotent. For outbound work, use bounded retries with backoff and distinguish transient from permanent failures.

#### 5. Quality, security, and accessibility requirements

- Enable nullable reference types and implicit usings; treat compiler warnings as errors unless a documented dependency requires a narrow exception.
- Use async APIs end to end and honor cancellation tokens for I/O and background work.
- Validate untrusted input at the boundary and encode output. Protect against CSRF, open redirects, injection, insecure direct object references, and over-posting.
- Store no secrets in Git. Use ASP.NET Core configuration, user-secrets for local development, and the deployment platform's secret store for production.
- Add structured, actionable logs without tokens, credentials, message contents, or unnecessary personal information.
- Ensure keyboard navigation, visible focus, semantic labels, validation announcements, sufficient contrast, and reduced-motion support. Target WCAG 2.1 AA.
- Retain or improve source test coverage for core business rules, authorization boundaries, API compatibility, persistence, and SMS success/failure/idempotency paths.
- Do not suppress failing tests or weaken assertions to obtain a green build.

#### 6. Documentation and operations

Update the target `README` with:

- prerequisites and exact restore/build/test/run commands;
- local configuration and a complete environment-variable table (name, purpose, required/optional, safe example; never a real secret);
- database creation and migration steps;
- authentication and role/permission setup;
- SMS provider and webhook configuration;
- consolidated architecture and mapping from each former service;
- deployment, health-check, backup/restore, and rollback instructions;
- known differences from the source, if any.

Add the traceability matrix and architecture decision record under `docs/`. Include an example configuration file containing safe dummy values only.

### Verification and acceptance criteria

The work is complete only when all of the following are true:

1. The target has exactly one production Blazor `.csproj` and starts as one process.
2. No runtime dependency on a deployed `SentryApp`, `SentryGate`, or `SentrySMS` remains.
3. Every confirmed source capability appears in the traceability matrix and has been implemented and verified, or is explicitly documented as an owner-approved exclusion.
4. Existing public routes, API contracts, persistence semantics, SMS workflows, and permission rules behave equivalently, with contract tests where practical.
5. Anonymous, ordinary-user, elevated-user, cross-account/tenant, and administrator authorization cases are tested, including expected denials.
6. A clean checkout can restore, build, test, and run by following the `README` without undocumented manual steps.
7. Formatting, static analysis, dependency/security audit, automated tests, and a production configuration build pass.
8. A browser smoke test verifies critical journeys at desktop and mobile widths, including loading, empty, validation, success, and failure states.
9. No secret, credential, personal data, source build artifact, or unnecessary generated file is committed.
10. The final pull request contains a concise architecture summary, service-to-module mapping, migration notes, exact test commands/results, screenshots for visible UI changes, risks, and known limitations.

### Completion protocol

- Review the final diff for accidental behavior changes and sensitive information.
- Run all documented checks from a clean state.
- Commit logical changes with clear messages on the feature branch and push it to the target repository.
- Open a pull request; do not claim completion without a commit and pull request.
- In the final report, list changed files, summarize behavior parity, provide the source and target commit SHAs used, and report each verification command with its actual outcome. Clearly distinguish a passing check from one skipped because of an environmental limitation.

Do not stop after scaffolding, an architecture plan, or a partial proof of concept. Continue until the acceptance criteria are met, unless blocked by unavailable access or a decision that only the repository owner can make. If blocked, provide the exact failing command/resource, work completed, remaining traceability items, and the smallest concrete owner action needed to proceed.

---
