# Session Handoff

> Read this AFTER `CLAUDE.md` (auto-loaded) and BEFORE issuing the next
> instruction. Updated by Claude at sprint boundaries and on user request.

---

## Where we are

- **Sprint:** 2 (Expense CRUD + Frontend Integration) — not started yet
- **Current activity:** user is performing a manual code review of Sprint 1
  before resuming. Findings are expected to land as `Human review (AI blind
  spot)` cards in `docs/genai.md`.
- **Last commit on `main`:** `c00b715` `docs(audit): add four-path source taxonomy`
- **Status:** paused — awaiting user input

## Completed sprints

- ✅ **Sprint 1 — Foundation & Auth E2E** — 9 commits, 54 unit/integration tests
  green, full E2E smoke verified against the Podman-hosted SQL Server 2025
  container (register / login / `/auth/me` / wrong creds / dup email / CORS
  policy / security headers / health endpoints). Argon2id hash with OWASP
  parameters confirmed in DB.

## Open decisions / pending

- User is reading the Sprint 1 codebase as a human reviewer. Each substantive
  finding becomes an Issue Card in `docs/genai.md` tagged with
  `Human review (AI blind spot)` (see `CLAUDE.md` for the four source paths).
- Sprint 2 will start once user signals ready ("Sprint 2", "vamos", etc.).
- 7 existing Issue Cards from Sprint 1 cleanup pass might benefit from voice
  edits before final delivery — user flagged candidates: cards 1, 2, 7.

## Recent gotchas worth remembering

- **SqlClient + Podman:** always `tcp:127.0.0.1,1433`, never `localhost,1433`.
  IPv6 resolution path times out otherwise. Connection string already correct
  in `appsettings.Development.json` and `db/BallastLane.Migrations/Program.cs`.
- **JwtSettings divergence:** `Program.cs` reads JWT secret directly from
  `IConfiguration` at startup. The test factory's `StubTokenService` mirrors
  `appsettings.Development.json` verbatim — DO NOT introduce in-memory test
  overrides for the `Jwt` section.
- **Health check overrides** must use `services.PostConfigure<HealthCheckServiceOptions>(...)`
  to reach the registrations. Filtering DI for `HealthCheckRegistration` finds
  nothing.
- **`ValidationException`** in the codebase always means
  `BallastLane.Application.Common.ValidationException` — when both namespaces
  are imported, fully qualify as `Common.ValidationException` to disambiguate
  from `FluentValidation.ValidationException`.
- **Argon2id parameters** in `Argon2idPasswordHasher` are OWASP minimums for
  2024 (`m=19456`, `t=2`, `p=1`). Don't lower without re-benchmarking.

## Boot script for a fresh session

Run before doing anything else:

1. Confirm `CLAUDE.md` was auto-loaded (verify Workflow section's
   "never commit autonomously" rule is loaded).
2. Read this `_HANDOFF.md`.
3. `git log --oneline | head -15`
4. Read `docs/genai.md` quickly (especially the four-path Methodology and
   the existing Issue Cards).
5. Sanity: `dotnet test` (expect 54 passing across 5 projects;
   `BallastLane.ArchitectureTests` is empty by design until Sprint 3).
6. Sanity: `podman ps` (look for `ballastlane-sqlserver` Up + healthy).
   If not running: `podman compose up -d`.
7. **Wait for user instruction.** Do not auto-resume Sprint 2.

## What does NOT belong in this file

- Architectural decisions → those are in `docs/genai.md` Issue Cards or the
  per-commit messages.
- Test names / project structure → those are derivable from `git ls-files`
  and `dotnet test`.
- The plan that drove Sprint 1 → `~/.claude/plans/c-users-aliss-downloads-net-bla-spicy-tide.md`
  (still relevant for Sprint 2/3 reference).
