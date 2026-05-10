# Architecture — Ballast Lane Personal Expense Tracker

This document is the technical companion to the [README](../README.md). It
explains the architectural choices, the dependency model, and the trade-offs
considered, in the order a code reviewer would naturally walk the codebase.

---

## 1. Why Clean Architecture (and not Vertical Slice)

The exercise calls for "Clean Architecture", which the project takes
literally: four projects (`Domain`, `Application`, `Infrastructure`, `Api`)
with a strict dependency rule and a single composition root.

**Why this and not Vertical Slice?**

Vertical Slice is excellent when features are isolated and CQRS is a natural
fit — handlers are co-located with their DTOs, validators, and SQL. For this
take-home, the domain has a small, shared set of cross-cutting concerns
(authentication, ownership filter, validation pipeline, ProblemDetails error
mapping). Clean Architecture lets those concerns live in one place and be
shared by every use case without duplication.

**Why not DDD-heavy?**

The domain is two entities (`User`, `Expense`) with simple invariants. Going
heavy on aggregates, value objects for everything, and domain events would
read as overengineering for the size of the model. The entities use
constructor validation and `Hydrate`-style reconstruction for repository
mapping; that's the right amount of DDD for this scope.

---

## 2. Dependency model

```
        ┌─────────────┐
        │     Api     │ ─────┐  (composition root, DI wiring only)
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
```

| Layer | Depends on | NEVER references |
|---|---|---|
| `Domain` | nothing | everything else |
| `Application` | `Domain`, `FluentValidation` | `Microsoft.Data.SqlClient`, `Microsoft.AspNetCore.*` |
| `Infrastructure` | `Application`, `Domain`, ADO.NET, Konscious, Polly, JwtBearer | `Microsoft.AspNetCore.Mvc.*` |
| `Api` | `Application`, `Infrastructure` | bypassing the use-case layer to talk to a repository directly |

**Rule of iron:** the `Api` endpoint classes never `new SqlConnection(...)`.
Endpoints take a use case via DI, call its `HandleAsync`, and translate the
return value or exception into an HTTP result. The use case orchestrates
domain entities and repository interfaces; the implementations of those
interfaces live in `Infrastructure` and are wired only at `Program.cs`.

The dependency rule is enforced in code via the project file references
(`Application` simply has no `<PackageReference>` to SqlClient, so it can't
compile against it).

---

## 3. Layer responsibilities

### 3.1 Domain (`src/BallastLane.Domain`)

- **Entities:** `User`, `Expense`. Constructors are private; entities are
  created via factory methods (`User.Create`, `Expense.Create`) that validate
  invariants, and reconstructed from storage via `Hydrate(...)` factories
  that bypass validation (the database's CHECK constraints already guard
  the invariants on read).
- **Domain exceptions:** `DomainValidationException` (single base class for
  invariant violations) — caught by the global exception handler and mapped
  to HTTP 400.
- **Why no `field` keyword on validating setters?** An early attempt used the
  C# 14 `field` keyword for setter-level validation. This made `Hydrate` go
  through the setter on every reconstruction — any edge-case row in the DB
  would blow up reading. Documented as a Sprint 1 cleanup-pass card; the fix
  was to move validation into explicit `EnsureValidX(...)` helpers called
  from `Create` and `Update`, and have `Hydrate` skip them entirely.

### 3.2 Application (`src/BallastLane.Application`)

- **Use cases:** one class per business operation
  (`RegisterUserUseCase`, `LoginUseCase`, `CreateExpenseUseCase`,
  `ListExpensesUseCase`, `GetExpenseByIdUseCase`, `UpdateExpenseUseCase`,
  `DeleteExpenseUseCase`). Each has a single `HandleAsync(...)` method that
  takes the command, the caller's `userId`, and a `CancellationToken`.
- **Repository interfaces:** `IUserRepository`, `IExpenseRepository`. Every
  method that touches a user-owned resource takes `userId` as a required
  parameter (`GetByIdAsync(id, userId, ct)`, `DeleteAsync(id, userId, ct)`).
  The shape of the contract makes resource-level authorization impossible
  to violate by accident.
- **Validators:** FluentValidation-based, one per command, in their own
  files. Used by the use case via `IValidator<TCommand>` injection — keeps
  validation testable in isolation, decoupled from the endpoint.
- **Validator-side enum strictness:** `Enum.TryParse` is leniently spec'd
  (accepts `"Food,Transport"` as a flag combination, accepts `"999"` as a
  raw integer cast). The `CreateExpenseCommandValidator` uses a strict name
  match against `Enum.GetNames<ExpenseCategory>()` to fail closed instead.
- **Application exceptions:** `NotFoundException`, `ConflictException`,
  `ValidationException` (`AppException` base — note the rename from the
  initial `ApplicationException` to avoid shadowing `System.ApplicationException`).
- **Pagination:** `PagedResult<T>` is a simple record with computed
  `TotalPages`. The list use case clamps the requested page to ≥ 1 and the
  page size to (`DefaultPageSize=20`, `MaxPageSize=100`) before delegating
  to the repository.

### 3.3 Infrastructure (`src/BallastLane.Infrastructure`)

- **`SqlExecutor`** — a ~140-line helper that wraps `SqlConnection` /
  `SqlCommand` lifecycle and centralizes:
  - `ResiliencePipeline` (Polly v8): 3 retries with exponential backoff +
    jitter on transient `SqlException` numbers (timeout, network blip,
    deadlock, etc.). This is the deliberate substitute for what EF Core's
    `EnableRetryOnFailure` would have given us.
  - Four primitives: `OpenConnectionAsync`, `ExecuteNonQueryAsync`,
    `ExecuteScalarAsync<T>`, `ExecuteReaderAsync<T>`.
- **`UserRepository` / `ExpenseRepository`** — both implement the
  Application-layer interfaces via `SqlExecutor`. SQL is parameterized (no
  string concatenation with input). `ExpenseRepository` uses a single batch
  for paginated listing (one `SELECT COUNT(1)` and one `OFFSET/FETCH NEXT`
  in the same `SqlCommand`, read via `NextResultAsync`) to avoid two round
  trips. Defense in depth: `UPDATE` and `DELETE` always include
  `AND UserId = @userId` in the `WHERE` clause, even though the use case
  has already verified ownership via `GetByIdAsync(id, userId, ...)`.
- **`Argon2idPasswordHasher`** — Konscious wrapper with OWASP 2024
  parameters (`m=19456, t=2, p=1`); format is
  `argon2id$m=19456$t=2$p=1$<base64-salt>$<base64-hash>` so verification can
  parse the parameters and re-derive without external state.
- **`JwtTokenService`** — issues HS256 tokens with `sub`, `email`, `iat`,
  `exp` claims. The expiration is read from `JwtSettings`; the secret is
  loaded from `appsettings.Development.json` in dev (env var override in
  prod is the intended path — out of scope to wire now).
- **`SqlServerHealthCheck`** — runs `SELECT 1` via `SqlExecutor`; tagged
  `["ready", "db"]` so it shows up on `/health/ready` (readiness) and not
  on `/health` (liveness). The host's mere existence answers liveness.

### 3.4 Api (`src/BallastLane.Api`)

- **Endpoints** organized by feature in `MapGroup`s:
  - `AuthEndpoints.MapGroup("/auth")` — register, login, logout, csrf-token, me
  - `ExpensesEndpoints.MapGroup("/expenses")` — CRUD with `RequireAuthorization()`
- **Pipeline order** (in `Program.cs`):
  1. `UseSerilogRequestLogging`
  2. `UseMiddleware<SecurityHeadersMiddleware>`
  3. `UseMiddleware<ExceptionHandlingMiddleware>`
  4. (`UseHsts` + `UseHttpsRedirection` in Production only)
  5. `UseCors("BallastLaneSpa")`
  6. `UseAuthentication`
  7. `UseAuthorization`
  8. `UseAntiforgery`
  9. Endpoint maps + `MapOpenApi`
- **Cookie-based JWT** wiring: `AddJwtBearer` configures
  `OnMessageReceived` to lift the token out of the `ballastlane.auth`
  cookie (so the SPA never touches it from JavaScript) and `OnChallenge` to
  suppress `WWW-Authenticate` (we don't run HTTP Basic on the same realm).
- **Anti-CSRF enforcement** — `app.UseAntiforgery()` validates only
  endpoints whose metadata surfaces `IAntiforgeryMetadata` with
  `RequiresValidation = true`. Form-bound endpoints get this for free; for
  JSON-bodied endpoints, the metadata path turned out to be brittle in
  .NET 10 (verified by curl smoke), so anti-forgery is enforced via a
  `RequireAntiforgery()` extension method that attaches an
  `IEndpointFilter` calling `IAntiforgery.ValidateRequestAsync` on
  `POST/PUT/PATCH/DELETE`. Applied to `/expenses` (group level) and
  `/auth/logout`. `/auth/login` and `/auth/register` opt out via
  `.DisableAntiforgery()` — the user has no token before authenticating.
- **`ExceptionHandlingMiddleware`** — central translation from domain /
  application / antiforgery exceptions to RFC 7807 `ProblemDetails`:
  - `ValidationException` → 400 with `errors` field
  - `DomainValidationException` → 400
  - `InvalidCredentialsException` → 401
  - `NotFoundException` → 404
  - `ConflictException` → 409
  - `AntiforgeryValidationException` → 400
  - anything else → 500, stack trace included only in Development
- **`SecurityHeadersMiddleware`** — sets `X-Content-Type-Options`,
  `X-Frame-Options`, `Referrer-Policy`, `Content-Security-Policy`,
  `Permissions-Policy`; removes `Server` and `X-Powered-By`.
- **CORS named policy** `BallastLaneSpa` — explicit allowed origins from
  `Cors:AllowedOrigins` config; `AllowCredentials()` because the cookie
  needs it; exposes `XSRF-TOKEN` header so the SPA can read it.

---

## 4. Cross-cutting decisions

### 4.1 Authentication & authorization

- **Why JWT in HttpOnly cookie and not `Authorization: Bearer ... ` from
  `localStorage`?** XSS resistance. With `localStorage`, any injected script
  can read the token; with `HttpOnly` the browser refuses to expose it to
  JS. SameSite=Lax handles the bulk of CSRF cross-site cases automatically;
  the antiforgery token covers the rest.
- **Why no refresh tokens?** Out of the 72h envelope. Documented as a
  known limitation. Production would add refresh-with-rotation + family
  detection.
- **Resource-level authorization shape.** The repository interfaces
  literally don't expose `GetById(id)` — only `GetById(id, userId)`. The
  endpoint pulls `userId` from the JWT `sub` claim (with a fallback to
  `ClaimTypes.NameIdentifier` because the default `JwtBearer` pipeline
  remaps `sub` → `NameIdentifier` via the legacy
  `DefaultInboundClaimTypeMap`, a footgun documented in a Sprint 2 card).
  When user A asks for user B's expense, the repository returns `null`,
  the use case throws `NotFoundException`, and the endpoint returns
  **404 (not 403)** to avoid leaking the existence of the resource.

### 4.2 Validation in layers

| Layer | Validates | Tool |
|---|---|---|
| **Endpoint** | request body shape (model binding) | `RequestDelegate` |
| **Validator** | input syntax + value ranges | FluentValidation |
| **Use case** | business invariants that need cross-field reasoning | manual checks |
| **Domain entity** | invariants that must hold for any in-memory entity | constructor / `EnsureValid*` helpers |
| **Database** | last-resort check constraints | `CHECK (Amount > 0)`, `CHECK Category IN (...)` |

This is intentional defense in depth: a validator failure stops the request
before it hits the use case; a domain invariant failure stops it before it
hits the database; the database check exists so a bug in the C# layer
can't corrupt the on-disk shape.

### 4.3 Error handling

A single `ExceptionHandlingMiddleware` turns every thrown exception into a
ProblemDetails response. Use cases throw *typed* exceptions
(`ValidationException`, `NotFoundException`, etc.); the middleware does the
mapping. The endpoint code never has a `try/catch` — it just `await`s the
use case and returns the result, which keeps the endpoint two-or-three lines
long.

### 4.4 Resilience

`SqlExecutor` wraps every database call in a Polly v8 `ResiliencePipeline`
with retry-with-jitter on the 18 transient `SqlException` numbers. This
covers the same ground EF Core's `EnableRetryOnFailure` would have. A
circuit breaker is the natural next step (stops bashing a down database
after N consecutive failures); deliberately out of scope for the take-home,
but the pipeline-builder pattern means it's a one-line addition.

### 4.5 Observability

- **Serilog** configured in `Program.cs` with the console sink and
  request logging. Logs are JSON-shaped, enriched with `RequestId`. The
  `ExceptionHandlingMiddleware` logs at `Warning` for handled exceptions
  (4xx) and `Error` for unhandled (5xx), and never logs PII (the user
  email, password, password hash) in plain text.
- **Health endpoints** at `/health` (liveness) and `/health/ready`
  (readiness, runs `SELECT 1`). The check is tagged so a future
  multi-check setup can filter cleanly.

---

## 5. Frontend architecture

The Angular SPA in `web/` mirrors the backend's "small + boring" approach.

- **Angular 21**, zone-based change detection (zoneless was attempted then
  reverted in Sprint 2.6 because Angular Material's CDK overlays are not
  yet fully stable under zoneless — change documented in a card).
- **Standalone components, `ChangeDetectionStrategy.OnPush`, signals**
  for component-local state.
- **Reactive Forms** (Signal Forms is still maturing in 21).
- **Routing** is lazy-loaded (`loadComponent`) and gated by `canMatch`
  guards (`authGuard`, `anonymousGuard`).
- **Auth flow:** `AuthService` exposes a `currentUser` signal and
  `isAuthenticated` computed; `provideAppInitializer` calls
  `auth.refresh()` on boot, which hits `/auth/me` to detect an existing
  session. Login then re-fetches `/auth/csrf-token` so the antiforgery
  tokens are bound to the now-authenticated principal (a subtlety
  documented in the CSRF card).
- **HTTP plumbing:** a single `credentialsInterceptor` sets
  `withCredentials: true` on every request and copies the `XSRF-TOKEN`
  cookie value into the `X-XSRF-TOKEN` header on `POST/PUT/PATCH/DELETE`.
- **Form-field error timing:** a custom `SubmittedErrorStateMatcher`
  (provided globally) defers error visibility until the user clicks
  submit — the visual experience stays clean while typing. Documented
  as a card.
- **Amount input:** the New / Edit Expense dialog uses a custom
  cash-register input (`<input type="text" inputmode="decimal">` with a
  manual keydown / paste handler). Locks 2-decimal display, period as the
  decimal separator regardless of browser locale, and "type the cents
  first" entry semantics. Documented as a card.

---

## 6. Data model

Two tables, one foreign key, one index.

```sql
-- Users
Id              UNIQUEIDENTIFIER  PRIMARY KEY DEFAULT NEWID()
Email           NVARCHAR(256)     NOT NULL  UNIQUE
PasswordHash    NVARCHAR(500)     NOT NULL
CreatedAt       DATETIME2(3)      NOT NULL DEFAULT SYSUTCDATETIME()

-- Expenses
Id              UNIQUEIDENTIFIER  PRIMARY KEY DEFAULT NEWID()
UserId          UNIQUEIDENTIFIER  NOT NULL  FOREIGN KEY → Users(Id) ON DELETE CASCADE
Amount          DECIMAL(18, 2)    NOT NULL  CHECK (Amount > 0)
Description     NVARCHAR(500)     NULL
Category        NVARCHAR(50)      NOT NULL  CHECK (Category IN (...))
IncurredAt      DATETIME2(3)      NOT NULL
CreatedAt       DATETIME2(3)      NOT NULL DEFAULT SYSUTCDATETIME()

INDEX IX_Expenses_UserId_IncurredAt (UserId, IncurredAt DESC)
```

**Choices and defenses:**

- `UNIQUEIDENTIFIER` (GUID) PKs — avoids ID enumeration in URLs
  (`/expenses/123` would leak count); cost is 12 extra bytes per row, which
  is irrelevant at this scale. In a high-write production system I'd reach
  for sequential GUIDs or ULIDs to avoid index fragmentation.
- `NEWID()` server-side default — client doesn't need to think about ID
  generation; both `Domain.Expense.Create` and the SQL DEFAULT can produce
  one, and they don't collide.
- `Category` as a string with a CHECK constraint, not a foreign key to a
  `Categories` table. In production each user might curate their own
  categories (FK + per-user table); for the take-home a fixed list of 7
  categories matches the domain enum and keeps the schema flat.
- `IncurredAt` separate from `CreatedAt` so users can log retroactive
  expenses. Datetimes are UTC at every boundary
  (`SYSUTCDATETIME()` in SQL, `DateTime.UtcNow` in C#, the SPA normalizes
  the date picker via a small helper documented in a card).
- `ON DELETE CASCADE` — deleting a user deletes their expenses. In
  production this would be a soft-delete (audit trail / GDPR right-to-
  erasure trade-off); for a demo, hard delete is fine.

---

## 7. Trade-offs explored

Decisions made deliberately, with the alternative documented for the
record. The full ADR set is in [`adr/`](./adr/).

- **ADO.NET vs Dapper / micro-ORM** — banned by the exercise; even without
  the ban, ADO.NET demonstrates the fundamentals (connection lifecycle,
  parameterization, mapping) that micro-ORMs abstract. The `SqlExecutor`
  helper centralizes the plumbing without becoming an ORM itself.
- **JWT-in-cookie vs `localStorage` Bearer** — XSS defense in depth
  (HttpOnly cookies are inaccessible to JavaScript); SameSite=Lax handles
  most CSRF; antiforgery covers the rest.
- **Argon2id (Konscious) vs BCrypt vs BCL** — Argon2id is the OWASP gold
  standard since 2015 (memory-hard, GPU-resistant). The .NET BCL doesn't
  ship a public Argon2id API; Konscious is the mature community option.
- **Angular 21 zone-based vs zoneless** — zoneless is the future but
  Material's CDK overlays still drag legacy in 21; an attempted zoneless
  setup produced flaky behavior in `mat-select` and `mat-datepicker`
  during early Sprint 2 work, reverted to zoneful for stability.
- **Minimal APIs with `MapGroup` vs Controllers** — Minimal APIs are the
  default mainstream in .NET 10. `MapGroup` per feature gives the same
  separation as Controllers with less reflection and a smaller cold start;
  `IEndpointFilter` covers what MVC filters used to.
- **Polly v8 retry vs no retry** — without an ORM, EF Core's
  `EnableRetryOnFailure` is gone. Polly's `ResiliencePipeline` is the
  declarative, testable substitute and the de-facto standard in .NET 10.
- **Conventional Commits in English imperative** — the `git log` reads
  as a TDD storyline (`test: cover X` → `feat: implement X` →
  `refactor: ...`); imperative mood is the open-source default and the
  consensus in international teams.
- **Grate vs DbUp / FluentMigrator** — Grate is the modern DbUp:
  port of RoundhousE, idempotent by script hash, CI/CD-friendly out of
  the box. FluentMigrator's fluent C# would have read as ORM-ish, which
  the exercise bans by spirit if not by letter.

---

## 8. What's intentionally NOT here

- **Refresh tokens.** Access-only JWT, 1h TTL. Re-login on expire.
- **Per-user category curation.** Fixed enum.
- **Outbox pattern, integration events, message bus.** Single-process
  CRUD; no cross-service work.
- **OpenTelemetry distributed tracing.** Single-process; would be wired
  the moment a second service appeared.
- **Caching (Redis), rate limiting, API versioning.** Recognized as
  production hardening, all out of scope for the 72h envelope.
- **CI pipeline.** Out of scope; the test suite is structured so a
  GitHub Actions workflow is a one-file addition.

The full GenAI audit trail of corrections applied while building the above
is at [`genai/issues.md`](./genai/issues.md).
