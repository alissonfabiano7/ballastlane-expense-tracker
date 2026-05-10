# Ballast Lane — Personal Expense Tracker

> **User story:** As a person who cares about personal finances, I want to record
> and categorize my expenses so I can visualize where my money is going and
> identify where I can save.

A take-home solution for the Ballast Lane Senior .NET Engineer position. Built
on Clean Architecture principles with TDD, modern .NET 10 stack, and a
responsive Angular SPA. Two tables (Users + Expenses), CRUD with resource-level
authorization, JWT-in-HttpOnly-cookie auth with anti-CSRF, parameterized SQL
without an ORM.

---

## ⚡ Quick Start

**Prerequisites:** .NET 10 SDK · Node 20+ · Docker 24+ **or** Podman 5+ · git

```sh
# 0. One-time — restore the .NET tools the migrations runner depends on (`grate`).
dotnet tool restore

# 1. SQL Server 2025 — bring the database container up.
docker compose up -d --wait          # `--wait` blocks until the SQL healthcheck passes (~30s on first boot)
# Podman users: prefix with `podman machine start` on macOS/Windows (safe to re-run if already started),
# then replace `docker compose` with `podman compose` — the compose.yml is portable across both.

# 2. Apply schema + seed — creates demo user + 8 sample expenses. Idempotent on re-run.
dotnet run --project db/BallastLane.Migrations

# 3. Backend — terminal 1
dotnet run --project src/BallastLane.Api
# → http://localhost:5080  (port pinned by Properties/launchSettings.json,
#    which also sets ASPNETCORE_ENVIRONMENT=Development so appsettings.Development.json loads)

# 4. Frontend — terminal 2
cd web
npm install                          # first time only (~60s)
npm start                            # alias for `ng serve`
# → http://localhost:4200  (the dev server proxies /auth, /expenses, /health to :5080)
```

Sign in at <http://localhost:4200>:

- **Email:** `demo@ballastlane.test`
- **Password:** `Demo@123`

You should see 8 sample expenses across all categories (Food, Transport,
Housing, Leisure, Health, Education, Other). Re-running step 2 is a no-op
thanks to Grate's hash-based idempotence.

---

## 📖 How to read this repo

If you're reviewing this take-home, here's the map of the thought-process
artifacts:

- **You're here** — README: high-level decisions, stack, security baseline,
  ADR index, troubleshooting.
- [`docs/architecture.md`](./docs/architecture.md) — the architecture
  walkthrough: four layers, the dependency rule, what each project is
  and is NOT allowed to know, and the cross-cutting decisions (auth,
  validation, error handling, resilience, observability).
- [`docs/adr/`](./docs/adr/) — eight one-page ADRs, one decision each
  (ADO.NET vs ORMs, Argon2id vs BCrypt, JWT-cookie vs localStorage,
  Minimal APIs vs Controllers, Polly retry, Grate, Conventional Commits,
  Angular 21 zone-based).
- [`docs/genai.md`](./docs/genai.md) — the GenAI deliverable headline:
  the seed prompt, audit-trail summary, and final reflection on what
  Claude got right, what it got wrong, and how the live trigger-based
  audit compared to a sandboxed single-shot experiment.
- [`docs/genai/issues.md`](./docs/genai/issues.md) — the live audit trail:
  20 issue cards documenting every correction applied to AI-generated
  code, organized into "AI self-audit" and "human-discovered" sections.
- [`docs/genai/planning-session.md`](./docs/genai/planning-session.md) —
  sanitized planning artifact from the Claude Code planning session that
  ran before the first line of code. Reads as evidence of planning-first
  rather than vibe-coding.
- [`CLAUDE.md`](./CLAUDE.md) — the project conventions read by Claude at
  every session: language rule, commit style, the GenAI Audit Trail
  directive that governs `docs/genai/issues.md`.

A reviewer with 5 minutes can stop at this README. A reviewer with 30
minutes will find more depth in `architecture.md` and the audit trail.

---

## 🏗️ Architecture

Four-project Clean Architecture with a strict dependency rule (each arrow
points from caller to callee; nothing in the inner layers references anything
outer):

```
        ┌─────────────┐
        │     Api     │ ─────┐  (composition root, DI)
        └──────┬──────┘      │
               │             │
               ▼             ▼
        ┌─────────────┐  ┌──────────────────┐
        │ Application │ ◀│  Infrastructure  │
        └──────┬──────┘  └──────────────────┘
               │
               ▼
        ┌─────────────┐
        │   Domain    │
        └─────────────┘

Domain          → no external dependencies (pure)
Application     → Domain + FluentValidation
Infrastructure  → Application + Domain + ADO.NET + Konscious + Polly + Grate
Api             → Application + Infrastructure (only at Program.cs)
```

**Rule of iron:** the `Api` layer (Minimal API endpoint classes) never
references `Microsoft.Data.SqlClient`. The `Domain` layer never references
ASP.NET, Microsoft.Data.SqlClient, or any cross-cutting concern. See
[`docs/architecture.md`](./docs/architecture.md) for the full walkthrough,
including the use-case pattern, the `SqlExecutor` helper that replaces what
EF's `EnableRetryOnFailure` would have given us, and the trade-offs against
Vertical Slice / DDD-heavy alternatives.

The planning artifact that produced this architecture is preserved in
[`docs/genai/planning-session.md`](./docs/genai/planning-session.md) — output
of a Claude Code planning session run before the first line of code.

---

## 🧱 Tech Stack

| Layer | Choice |
|---|---|
| Runtime | **.NET 10 LTS** (released Nov/2025, support through Nov/2028) |
| Language | **C# 14** — uses `field` keyword and extension members where they reduce boilerplate |
| API style | **Minimal APIs with `MapGroup`** + `IEndpointFilter` (mainstream default in .NET 10) |
| Data access | **Plain ADO.NET** (`Microsoft.Data.SqlClient`) wrapped in a thin `SqlExecutor` helper with Polly retry pipeline. No ORM (constraint of the exercise). |
| Migrations | **Grate** — port of RoundhousE, idempotent by script hash, CI/CD-friendly |
| Password hashing | **Argon2id** via `Konscious.Security.Cryptography` with OWASP 2024 parameters (`m=19456, t=2, p=1`) tuned to ~250ms |
| Auth delivery | **JWT in HttpOnly cookie** (HS256, 1h TTL) + **anti-CSRF** via `Microsoft.AspNetCore.Antiforgery` with `X-XSRF-TOKEN` header |
| Validation | **FluentValidation** (validators isolated and unit-testable) |
| Logging | **Serilog** (console JSON sink) with `RequestId` enrichment |
| Resilience | **Polly v8** `ResiliencePipeline` in `SqlExecutor`: 3 retries with exponential backoff + jitter on transient `SqlException` |
| Health checks | **`Microsoft.Extensions.Diagnostics.HealthChecks`** with custom `IHealthCheck` running `SELECT 1` via ADO.NET |
| Tests | **xUnit + NSubstitute + Shouldly** (FluentAssertions v8 went commercial in Jan/2025; Shouldly is the MIT alternative) |
| Frontend | **Angular 21** (zone-based, OnPush + signals everywhere), Angular Material, Reactive Forms |
| Database | **SQL Server 2025** (`mcr.microsoft.com/mssql/server:2025-latest`) via Docker (Podman compatible — same `compose.yml`) |

---

## 🛡️ Security baseline

Treated as default engineering, not stretch:

- **CORS named policy** `BallastLaneSpa` with explicit allowed origins +
  `AllowCredentials()` (no `AllowAnyOrigin` anywhere, even in Development —
  combined with `AllowCredentials` it would be a CSRF receipt).
- **Security headers** via `SecurityHeadersMiddleware`:
  - `X-Content-Type-Options: nosniff`
  - `X-Frame-Options: DENY`
  - `Referrer-Policy: strict-origin-when-cross-origin`
  - `Content-Security-Policy: default-src 'self'`
  - `Permissions-Policy: geolocation=(), microphone=(), camera=()`
- **`Server: Kestrel` header removed** (`AddServerHeader = false`).
- **HSTS + HTTPS redirection** in Production.
- **Argon2id** (memory-hard, GPU-resistant) over BCrypt or PBKDF2; parameters
  in source so they can be re-tuned per hardware.
- **JWT HS256, 1h TTL, in HttpOnly + Secure + SameSite=Lax cookie** — token
  is inaccessible to JavaScript (XSS defense in depth).
- **Anti-CSRF enforced** on every mutating endpoint via a small
  `RequireAntiforgery()` extension that runs `IAntiforgery.ValidateRequestAsync`
  as an `IEndpointFilter`. `/auth/login` and `/auth/register` opt out via
  `.DisableAntiforgery()` (anonymous bootstraps).
- **100% parameterized SQL** — every `SqlCommand` uses
  `Parameters.Add(...)` with explicit `SqlDbType`. No string concatenation
  with user input anywhere.
- **Resource-level authorization** — every repo method that touches a
  user-owned resource takes `userId` as a required parameter
  (`GetByIdAsync(id, userId, ...)`, `DeleteAsync(id, userId, ...)`); when
  user A asks for user B's expense the API returns **404, not 403** to
  avoid leaking the existence of the resource.

## ❤️ Health & resilience

- `GET /health` — **liveness** (always 200 while the host is up).
- `GET /health/ready` — **readiness** (executes `SELECT 1` against SQL Server
  via the same `SqlExecutor`; returns 503 if the database is unreachable).
- Polly v8 `ResiliencePipeline` in the `SqlExecutor`: 3 retries, exponential
  backoff `200ms → 400ms → 800ms` with jitter, fires on the 18 transient
  `SqlException` numbers (timeouts, network blips, transient deadlocks). This
  is the deliberate substitute for EF Core's `EnableRetryOnFailure`.

---

## 🎯 Design decisions

Each ADR is a one-page document with context + decision + consequences:

- [ADR-001 — ADO.NET over micro-ORMs](./docs/adr/0001-ado-net-over-micro-orms.md)
- [ADR-002 — JWT in HttpOnly cookie + anti-CSRF (no localStorage)](./docs/adr/0002-jwt-cookie-anti-csrf.md)
- [ADR-003 — Argon2id (Konscious) over BCrypt / BCL](./docs/adr/0003-argon2id-konscious.md)
- [ADR-004 — Angular 21 zone-based (zoneless deferred)](./docs/adr/0004-angular-21-zone-based.md)
- [ADR-005 — Minimal APIs with MapGroup over Controllers](./docs/adr/0005-minimal-apis-mapgroup.md)
- [ADR-006 — Polly retry pipeline as ORM-substitute resilience](./docs/adr/0006-polly-retry-pipeline.md)
- [ADR-007 — Conventional Commits in English imperative](./docs/adr/0007-conventional-commits.md)
- [ADR-008 — Grate over DbUp / FluentMigrator](./docs/adr/0008-grate-migrations.md)

---

## 🧪 Running tests

```sh
dotnet test                                       # all 112 backend tests
dotnet test --collect:"XPlat Code Coverage"       # with coverage
```

**Current coverage:** 18 Domain + 61 Application + 10 Infrastructure + 23 Api =
**112 backend tests, all passing**. The Application layer is the TDD focus
(visible in `git log` as `test: ... → feat: ...` pairs through Sprint 1.4 and
Sprint 2.2). Infrastructure repository tests are deferred to a Sprint-4 stretch
goal (TestContainers); the SQL is exercised end-to-end by the Api integration
tests that hit the in-memory repository, plus manual smoke tests against the
real database via the seed migration.

---

## 📁 Project structure

```
src/
├── BallastLane.Domain/              ← entities (User, Expense), domain exceptions
├── BallastLane.Application/         ← use cases, repository interfaces, validators
├── BallastLane.Infrastructure/      ← ADO.NET impls, JWT, Argon2id, Polly, health checks
└── BallastLane.Api/                 ← Minimal APIs, middleware, composition root
tests/
├── BallastLane.Domain.Tests/        ← pure unit tests (18)
├── BallastLane.Application.Tests/   ← TDD focus: NSubstitute + Shouldly (61)
├── BallastLane.Infrastructure.Tests/← Argon2id + SqlExecutor pipeline (10)
└── BallastLane.Api.Tests/           ← WebApplicationFactory integration tests (23)
db/
├── BallastLane.Migrations/          ← Grate runner
└── scripts/up/                      ← 0001_init.sql + 0002_seed.sql (idempotent)
web/                                 ← Angular 21 SPA (Material + signals)
docs/
├── architecture.md                  ← decisions + diagrams + dependency walk
├── adr/                             ← 8 ADRs (one decision each)
├── genai.md                         ← GenAI deliverable headline
└── genai/
    ├── issues.md                    ← live audit trail of corrections (append-only)
    └── planning-session.md          ← sanitized planning artifact
```

---

## 🤖 GenAI section

This project was built with Claude Code as an AI pair. The deliverable headline
lives at [`docs/genai.md`](./docs/genai.md) — prompt, representative output,
audit trail summary, and final reflection.

The detailed audit trail is at
[`docs/genai/issues.md`](./docs/genai/issues.md) — every correction applied
during development, organized into two sections by source:

- **Cleanup-pass cards** (11 entries, five-section template, empty
  `Human revalidation` by convention) — issues surfaced by Claude reading git
  diffs at sprint boundaries.
- **Human-discovered cards** (9 entries, three-section template, written
  entirely in the developer's voice) — issues caught through direct usage of
  the running app or manual code review. The strongest authenticity signal in
  the audit trail.

The card template, four trigger paths, and the binary that distinguishes
"AI self-audit" from "human-driven catch" are all defined in the project's
[`CLAUDE.md`](./CLAUDE.md) — read by Claude Code at the start of every session.

---

## ⚠️ Known limitations / out of scope

Defended in the live presentation if asked:

- **Refresh tokens** — access-only JWT (1h TTL). User re-logs on expiry. In
  prod: refresh with rotation + family detection.
- **Email confirmation, forgot password** — out of scope.
- **Roles / permissions / multi-tenancy** — single role, single tenant.
- **Caching (Redis), rate limiting, API versioning, OpenTelemetry** — all
  recognized as production hardening, all out of scope for the 72h envelope.
- **`provideAppInitializer` blocks SPA boot** if `/auth/me` hangs because the
  API is unreachable — known gap, fix proposed (RxJS `timeout(5000)` on the
  `auth.refresh()` call). Not yet shipped.
- **Domain-level UTC kind enforcement on `IncurredAt`** — the API client
  normalizes to UTC at the boundary (cash-register input + helper that sends
  current UTC moment for "today" and noon UTC for past dates), but the domain
  entity does not yet `EnsureUtc(...)` on construction. Tracked as Sprint 2.1
  in the planning artifact, deferred per developer direction.
- **Stretch goals not pursued:** TestContainers integration tests for the
  repositories, CI via GitHub Actions.

---

## 🛠️ Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `docker compose up` / `podman compose up` errors with "Cannot connect to Docker daemon" or "Cannot connect to Podman" | Container runtime not running. On macOS/Windows: Docker Desktop not started, or (Podman) `podman machine` VM not started | Start Docker Desktop, or run `podman machine start` |
| Migrations hang for ~30s and then fail with "wait operation timed out" | The connection string used `Server=localhost,1433`; the IPv6 path doesn't reach the container runtime's IPv4 listener (most common with Podman, also seen on some Docker Desktop setups) | Already fixed — connection string is `tcp:127.0.0.1,1433`. If you cloned and changed it, revert. |
| `dotnet run --project src/BallastLane.Api` errors with "Sql:ConnectionString is required" or "Jwt:Secret must be at least 32 characters" | `ASPNETCORE_ENVIRONMENT` is not `Development`, so `appsettings.Development.json` is not loaded | The committed `Properties/launchSettings.json` sets this for you. If it's missing, `set ASPNETCORE_ENVIRONMENT=Development` (Windows) or `export ASPNETCORE_ENVIRONMENT=Development` (Unix) before `dotnet run`. |
| `ng serve` errors with "Could not resolve @angular/animations/browser" | Material was added but `@angular/animations` peer wasn't pulled in | `npm install @angular/animations@^21.2.0 --save` (already in `package.json` after the fix). |
| `POST /expenses` returns `400 Invalid or missing anti-forgery token.` | The client did not send `X-XSRF-TOKEN` header | The Angular SPA's `credentialsInterceptor` reads the `XSRF-TOKEN` cookie and sets the header automatically. From `curl`, fetch `/auth/csrf-token` after `/auth/login` and pass the cookie value as `-H "X-XSRF-TOKEN: <value>"`. |
| Demo user can't sign in after re-running migrations | The Argon2id hash literal in `0002_seed.sql` drifted from the hasher's format | Guarded by `Argon2idPasswordHasherTests.Verify_known_seed_hash_succeeds` — if that test fails, the seed needs regeneration. |

---

## 📜 Conventions

- **Conventional Commits in English imperative** (`feat:`, `fix:`, `test:`,
  `refactor:`, `docs:`, `chore:`) — `git log --oneline` reads as a TDD
  storyline (`test: cover X` → `feat: implement X` → `refactor: ...`).
- **All async methods accept and propagate `CancellationToken`.**
- **All SQL is parameterized.** No string concatenation with user input.
- **Datetimes are UTC at boundaries.** `DateTime.UtcNow` in C#,
  `SYSUTCDATETIME()` in SQL.
- **Authorization at resource level.** Every repo method that touches a
  user-owned resource takes `userId` as a required parameter — there is no
  `GetById(id)`, only `GetById(id, userId)`. The resource ownership rule is
  impossible to violate by accident.

The full project conventions are in [`CLAUDE.md`](./CLAUDE.md) at the
repository root, including the GenAI Audit Trail directive that governs how
[`docs/genai/issues.md`](./docs/genai/issues.md) is maintained.
