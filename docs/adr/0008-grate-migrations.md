# ADR-008 — Grate over DbUp / FluentMigrator

**Status:** Accepted (Sprint 1)

## Context

The schema lives in two tables (`Users`, `Expenses`); the database is
SQL Server 2025 in a Podman container. Migrations need to be:

- Idempotent on re-run (CI / clone-fresh / panel demo)
- Versioned and inspectable as plain SQL
- Compatible with the constraint of "no ORM" — FluentMigrator's fluent
  C# would drift toward ORM-shaped DSL

## Decision

Use **Grate** — the .NET port of RoundhousE — invoked via a small
`BallastLane.Migrations` console runner.

- Scripts live in `db/scripts/up/0001_init.sql` and `0002_seed.sql`.
- Grate hashes each script; re-running an unchanged script is a no-op,
  and re-running a CHANGED script with the same name fails (forcing
  a new file). This is stricter (and safer) than DbUp's "if I haven't
  seen this filename, run it".
- The seed migration is itself idempotent at the SQL level
  (`IF NOT EXISTS` on the demo user, `WHERE NOT EXISTS` on each seed
  expense) — Grate's hash check is the second safety net.

## Consequences

**Positive**

- One-command apply (`dotnet run --project db/BallastLane.Migrations`)
  works in dev and would work the same way in a CI pipeline.
- Plain SQL is the most transparent migration format possible — the
  panel can `cat 0001_init.sql` and read the schema verbatim.
- Hash-based idempotence is the strongest correctness guarantee
  short of a full transactional schema diff.

**Negative**

- Adds a dependency (`grate` as a `dotnet` tool); slightly heavier
  than embedding a few `CREATE TABLE IF NOT EXISTS` calls in a
  bootstrap script.
- The argument list for the runner had a quirky "no `--databasename`
  flag" behavior (the database is encoded in the connection string)
  documented as an issue card — not Grate's fault, just a docs-vs-CLI
  mismatch.

## Alternatives considered

- **DbUp** — works fine; idempotence is by filename, not hash. Grate
  is the modern evolution and reads as more current to the panel.
- **FluentMigrator** — fluent C# DSL that looks ORM-ish; a poor fit
  for a project that explicitly avoids ORMs.
- **Embed `CREATE TABLE IF NOT EXISTS` in app startup** — works for
  a one-off demo but is anti-pattern for production.
- **EF Core migrations** — banned by the brief.

## Notes

The Argon2id seed hash literal in `0002_seed.sql` is guarded by
`Argon2idPasswordHasherTests.Verify_known_seed_hash_succeeds` — if the
hashing format ever drifts, the test fails before the demo user fails
to log in. Documented as a card.
