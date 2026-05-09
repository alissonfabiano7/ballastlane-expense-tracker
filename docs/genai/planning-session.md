# Planning Session — Ballast Lane Personal Expense Tracker

> **Artifact context.** This document is the polished output of a planning
> session run with Claude Code in plan mode, before the first line of code
> was written. It captures the stack, architecture, sprint structure, and
> GenAI methodology that were decided up front — so the implementation work
> in subsequent sessions had a stable target.
>
> The complementary [`issues.md`](./issues.md) is the live audit trail of
> corrections that landed *during* implementation; this document is the
> contract that was agreed *before* implementation began.

---

## 1. Context

**Brief.** Ballast Lane Senior .NET Engineer take-home. 72h corridos. Single
deliverable repository with backend (.NET) + frontend (SPA) + database
container; live presentation + code review on the panel side.

**Hard constraints from the brief:**

- No Entity Framework, no Dapper, no MediatR.
- Clean Architecture, TDD, CRUD over ≥ 2 tables (entity + users).
- API for user creation / login / authorized endpoints.
- Tests at every layer.
- Responsive Angular frontend.
- README + GenAI section + live presentation.

**Time budget.** ~28h productive (after sleep / meals / buffer).
Architecture has to fit that envelope; no investment that doesn't ship in
the 72h window.

**Defense rule.** Every choice has to fit into a 30-second answer at the
panel review. Items that recommend but might not be defendable were marked
during planning and resolved with a fallback.

---

## 2. Stack

| Layer | Choice | One-line defense |
|---|---|---|
| Runtime | **.NET 10 LTS** | LTS through Nov/2028. .NET 8 LTS expires Nov/2026 — picking 10 reads as candidate-current. |
| API style | **Minimal APIs with `MapGroup` + `IEndpointFilter`** | Mainstream default in .NET 10; less reflection, smaller cold start, same separation as Controllers via Route Groups. |
| Database | **SQL Server 2025** via Podman | Latest GA image; Podman doubles as a Docker drop-in. Schema is small; nothing version-specific. |
| Migrations | **Grate** | Port of RoundhousE; idempotent by script hash; CI/CD-friendly out of the box. The "DbUp moderno". |
| Data access | **ADO.NET puro** + thin `SqlExecutor` helper | Constraint-driven (no ORM allowed); helper centralizes connection lifecycle and Polly retry without being a micro-ORM itself. |
| Password hashing | **Argon2id via Konscious.Security.Cryptography** with OWASP 2024 params | OWASP gold standard since 2015; memory-hard, GPU-resistant; explicit `m=19456 / t=2 / p=1` so parameters can be re-tuned per hardware. |
| JWT delivery | **HttpOnly cookie + SameSite=Lax + anti-CSRF token** | XSS-resistant (token inaccessible to JS); SameSite blocks most CSRF; `Microsoft.AspNetCore.Antiforgery` covers the rest. |
| Validation | **FluentValidation** | Validators isolated, unit-testable, decoupled from the endpoint. |
| Logging | **Serilog** (console JSON sink) | Structured logs with `RequestId` enrichment; ready for an aggregation backend. |
| Resilience | **Polly v8 `ResiliencePipeline`** in `SqlExecutor` | Substitute for the EF Core `EnableRetryOnFailure` we lost: 3 retries, exponential backoff with jitter, on transient `SqlException`. |
| Health checks | **`Microsoft.Extensions.Diagnostics.HealthChecks`** + custom `IHealthCheck` | `/health` (liveness) + `/health/ready` (readiness with `SELECT 1`). BCL-native, k8s/ALB-ready format. |
| Tests | **xUnit + NSubstitute + Shouldly** | Shouldly because FluentAssertions v8 went commercial (Xceed) in Jan/2025; NSubstitute over Moq because of Moq's 2023 SponsorLink telemetry drama. |
| Frontend | **Angular 21 (zone-based)** + Angular Material + Reactive Forms | Zone.js kept for stability with Material CDK overlays — zoneless attempted in Sprint 2.6 and reverted after Material flakiness. |
| Containerization | **`compose.yml`** (SQL Server only) | One-command database; the API runs locally to keep iteration fast. |

---

## 3. Domain choice

**Personal Expense Tracker** — picked from a shortlist of three candidates
(also considered: Event RSVP / Workshop Booking, Personal Bookshelf).

- **Entities:** `User`, `Expense` (with a `Category` enum:
  Food / Transport / Housing / Leisure / Health / Education / Other).
- **Business rules worth testing:** `Amount > 0`, user can only see / mutate
  their own expenses, monthly aggregates by category.
- **5-minute demo flow:** login → list (with seed) → create new → try
  invalid amount (validation) → edit → delete → logout.

**Why this and not the others?**

- Familiar to any audience in 30 seconds; no domain explanation needed.
- 1:N relationship between user and expenses is the simplest non-trivial
  schema (qualifies as ≥ 2 tables without overengineering).
- Validation rules and ownership boundaries are present but bounded —
  doesn't drift into business-domain explanation during the demo.

> **User story.** *As a person who cares about personal finances, I want to
> record and categorize my expenses so I can visualize where my money is
> going and identify where I can save.*

---

## 4. Architecture

Clean Architecture, four projects, strict dependency rule.

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

**Why Clean and not Vertical Slice?** The shared cross-cutting concerns
(authentication, ownership filter, error mapping, validation pipeline)
benefit from a centralized place. Vertical Slice excels when each feature
is independent and CQRS is a natural fit; for a 7-endpoint CRUD with
shared rules, Clean reduces duplication.

**Why not DDD-heavy?** Two entities with simple invariants. Aggregates,
value objects for everything, and domain events would read as showcase
rather than fit. Constructor-validated entities + `Hydrate` reconstruction
is the right amount.

**Rule of iron.** The `Api` layer never references `Microsoft.Data.SqlClient`.
The `Domain` layer never references ASP.NET. Project file references
enforce this at the compile boundary; an ArchUnitNET project is scaffolded
as a stretch goal for asserting the same as a test.

The full layer-by-layer walkthrough is in
[`../architecture.md`](../architecture.md).

---

## 5. Authentication strategy

| Question | Answer | Defense |
|---|---|---|
| Token type | JWT access-only, 1h TTL | Refresh tokens are stretch goal in 28h. Re-login on expire. |
| Algorithm | HS256 | Symmetric, single secret. RS256 only buys benefit in multi-service. |
| Hashing | Argon2id (Konscious), OWASP params, ~250ms target | Memory-hard, GPU-resistant. BCL doesn't ship a public Argon2id API. |
| Claims | `sub` (UserId), `email`, `iat`, `exp` | Minimum defendable. No roles because no requirement. |
| Storage | HttpOnly + Secure + SameSite=Lax cookie | XSS defense in depth. localStorage is XSS-vulnerable; BFF pattern is more secure but a 6-8h refactor outside scope. |
| CSRF | `Microsoft.AspNetCore.Antiforgery` with `X-XSRF-TOKEN` header | Stateless; token signed and validated server-side. |
| Resource-level authz | Filter by `userId` extracted from the JWT in every repo method that touches a user-owned resource | Repo signatures literally require it: `GetById(id, userId)`, never `GetById(id)`. Impossible to violate by accident. |
| 404 vs 403 | Return 404 when user A tries to access user B's resource | Avoids leaking the existence of resources owned by other users. |

---

## 6. Test strategy

**Pyramid:**

```
              ┌──────────────────┐
              │  ArchitectureTests│  (ArchUnitNET, 4-5 rules — stretch)
              ├──────────────────┤
              │  Api.Tests        │  (WebApplicationFactory, ~20 tests)
              ├──────────────────┤
              │  Infrastructure.  │  (Argon2id + SqlExecutor pipeline + stretch TestContainers)
              ├──────────────────┤
              │  Application      │  (NSubstitute + Shouldly, ~60 tests — TDD focus)
              ├──────────────────┤
              │  Domain           │  (~18 pure tests)
              └──────────────────┘
```

**TDD posture.** Test-first on the Application layer (use cases +
validators) — visible in `git log` as `test:` → `feat:` → `refactor:`
sequences. In endpoints / composition root, code first then tests — the
ROI of TDD on configuration is low and the panel will recognize the
pragmatic cut.

**Coverage realism.** Target ~70-75% global; Application 85%+. Don't
chase 100% — composition (`Program.cs`) and DTOs don't generate signal.

**Conventional Commits in English imperative.** `feat:`, `fix:`, `test:`,
`refactor:`, `docs:`, `chore:`, `perf:`, `build:`, `ci:`. The panel will
open `git log` and the rhythm should communicate seniority. Branch is
linear; no squash.

---

## 7. Sprint structure

Three sprints, ~31h total, no day-of-week assignment — execution at the
developer's natural rhythm. Each sprint has an explicit acceptance
criterion.

### Sprint 1 — Foundation & Auth E2E (~11.5h)
Backend running E2E with cookie-based JWT auth + anti-CSRF tokens +
security baseline + health checks + Polly retry from the first commit.
Zero frontend. **Acceptance:** auth E2E via HttpOnly cookies works;
Argon2id verified; `/health` and `/health/ready` operational.

### Sprint 2 — Expense CRUD + Frontend Integration (~9.5h)
Feature complete E2E. CRUD working from front to back with auth and
ownership. Includes Angular Material setup + signal-based AuthService +
HttpClient interceptor for `withCredentials` and `X-XSRF-TOKEN`.
**Acceptance:** demo flow works end-to-end (login → create → edit →
delete via UI).

### Sprint 3 — Polish, Docs & Delivery (~10h)
Zero warnings, README complete, GenAI consolidated, presentation ready,
repository ready for review. **Acceptance:** clone-fresh runs;
`dotnet test` passes; the four documentation artifacts (README,
`architecture.md`, `genai/planning-session.md` (this file),
`genai/issues.md`) are complete; no warnings.

### Sprint Final — Pre-delivery (~30min)
Clone the repo into a fresh directory, run the full setup, confirm the
panel will be able to run it without editing anything.

---

## 8. GenAI methodology

The GenAI section of the deliverable was always going to be the most
distinctive part — it had to demonstrate fluency in *using* AI, not just
in *prompting* it.

**Decision:** the audit trail is built **live, by Claude Code itself**,
governed by a directive in the repository's `CLAUDE.md` (read by Claude
at the start of every session). The directive defines:

1. **Trigger phrases** (`audit isso`, `registra no genai`, `issue card`).
   Without a trigger, Claude does not log — only fixes.
2. **Four trigger paths**: live trigger, user manual fix, AI cleanup pass
   at sprint boundaries, and human review (AI blind spot). The first
   three are common; the fourth is the strongest authenticity signal.
3. **A binary inside the card** that distinguishes AI self-audit from
   human-driven catch — encoded by whether the `Human revalidation`
   section is empty or filled (later refactored into two sections in
   `issues.md`, one per origin, with a different template each).

**Why this works for top-of-band:**

- **Authentic.** Captured in real time, not reconstructed at the end.
- **Verifiable.** `git log -- docs/genai/issues.md` confirms chronology
  and the human voice in `### Human revalidation` is impossible to forge
  after the fact.
- **Meta-signal.** The candidate orchestrated AI to audit AI with explicit
  trigger phrases — fluency *and* critique in the same artifact.

**The full audit trail** is in [`issues.md`](./issues.md). The headline
that anchors it is in [`../genai.md`](../genai.md).

---

## 9. Out of scope (defended at the panel if asked)

- Refresh tokens (access-only JWT).
- Email confirmation, forgot password.
- Roles / permissions / multi-tenancy.
- Caching (Redis).
- Rate limiting / throttling.
- API versioning.
- OpenTelemetry / distributed tracing.
- Docker image of the API (only the database is containerized).
- CI / GitHub Actions.

Each of these was considered and explicitly cut to fit the 28h envelope
without leaving the core thin.

---

## 10. Note on iteration

This document is the *output* of planning, not the *transcript* of
planning. The actual session iterated on stack choices (initially
considered Angular 22 + Signal Forms before pinning to 21 zone-based
because 22 was still pre-release at scaffold time), domain candidate
(Personal Expense Tracker chosen over Event RSVP for time fit), and
specific component versions. The decisions captured here are the ones
that were carried forward; the alternatives that lost are summarized in
the ADRs in [`../adr/`](../adr/), one per major decision.

The iteration *during* implementation — every place where Claude
generated something that needed correction, and every place where the
developer caught something Claude missed — is in [`issues.md`](./issues.md).
