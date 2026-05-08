# Ballast Lane — Personal Expense Tracker

> **User story:** As a person who cares about personal finances, I want to record
> and categorize my expenses so I can visualize where my money is going and
> identify where I can save.

A take-home solution for the Ballast Lane Senior .NET Engineer position. Built
on Clean Architecture principles with TDD, modern .NET 10 stack, and a
responsive Angular SPA.

---

## Quick Start

> _Detailed setup instructions populated in Sprint 3._

```sh
# 1. Start the database
podman compose up -d   # or: docker compose up -d

# 2. Apply migrations
dotnet run --project db/BallastLane.Migrations

# 3. Run the API
dotnet run --project src/BallastLane.Api

# 4. (in another terminal) Run the frontend
cd frontend && npm install && ng serve
```

## Demo credentials

- **Email:** `demo@ballastlane.test`
- **Password:** `Demo@123`

---

## Architecture

> _Mermaid diagram + dependency rules populated in Sprint 3._

Four-project Clean Architecture: Domain → Application → Infrastructure → Api.
See [`docs/architecture.md`](./docs/architecture.md) for the full architectural
walkthrough, and [`docs/genai/00-planning-session.md`](./docs/genai/00-planning-session.md)
for the planning artifact produced before the first line of code.

---

## Tech Stack

- **Backend:** .NET 10 LTS, Minimal APIs with `MapGroup`, ADO.NET (no ORM),
  Grate for migrations, Argon2id (Konscious) for password hashing, JWT in
  HttpOnly cookie + anti-CSRF, FluentValidation, Serilog, Polly v8+ for
  retry resilience, BCL HealthChecks
- **Frontend:** Angular 22 (zone-based), Angular Material, Signal Forms,
  signals everywhere
- **Database:** SQL Server 2025 via Podman/Docker
- **Tests:** xUnit, NSubstitute, Shouldly, TestContainers, ArchUnitNET
- **C# 14 features:** `field` keyword, extension members where they reduce boilerplate

---

## Security baseline

- CORS named policy (no `AllowAnyOrigin`) with `AllowCredentials()`
- Security headers via middleware: `X-Content-Type-Options`, `X-Frame-Options`,
  `Referrer-Policy`, `Content-Security-Policy`, `Permissions-Policy`
- `Server: Kestrel` header removed (`AddServerHeader = false`)
- HTTPS redirection + HSTS in Production
- Argon2id with parameters tuned to ~250ms on target hardware
- JWT HS256 (1h TTL) delivered via HttpOnly + Secure + SameSite=Lax cookie
- Anti-CSRF via `Microsoft.AspNetCore.Antiforgery` with `X-XSRF-TOKEN` header
- 100% parameterized SQL (asserted by ArchUnitNET)

## Health & resilience

- `GET /health` — liveness (always 200 if app running)
- `GET /health/ready` — readiness (executes `SELECT 1` against SQL Server)
- Polly v8+ `ResiliencePipeline` in `SqlExecutor` with 3 retries, exponential
  backoff with jitter, for transient `SqlException`

---

## Running tests

```sh
dotnet test
dotnet test --collect:"XPlat Code Coverage"
```

## Project structure

```
src/         Domain, Application, Infrastructure, Api
tests/       xUnit projects per layer + ArchitectureTests
db/          Grate migration runner + scripts/up/*.sql
docs/        architecture.md, genai.md, adr/ (ADRs lite), genai/00-planning-session.md
frontend/    Angular 22 SPA
```

---

## GenAI section

See [`docs/genai.md`](./docs/genai.md) for the prompt, representative output,
and the live-maintained audit trail of corrections applied during development.

---

## Known limitations / out of scope

- Refresh tokens (access-only JWT, 1h TTL — re-login on expire)
- Email confirmation, forgot password
- Roles / permissions / multi-tenancy
- Caching (Redis)
- Rate limiting
- API versioning
- OpenTelemetry distributed tracing
- Docker image of the API (only the database is containerized)

## Troubleshooting

- _(populated in Sprint 3)_
