# ADR-001 — ADO.NET over micro-ORMs

**Status:** Accepted (Sprint 1)

## Context

The exercise brief explicitly bans Entity Framework, Dapper, and MediatR.
That removes the obvious choices for data access in a modern .NET project
and forces a deliberate decision about how to talk to SQL Server without an
ORM, while keeping connection lifecycle, parameterization, and resilience
right.

## Decision

Use **plain ADO.NET** (`Microsoft.Data.SqlClient`) wrapped in a thin
`SqlExecutor` helper (~140 lines) that centralizes:

- `using SqlConnection` / `using SqlCommand` lifecycle
- Polly v8 `ResiliencePipeline` for retry-with-jitter on transient failures
  (the substitute for EF Core's `EnableRetryOnFailure`)
- Four primitives: `OpenConnectionAsync`, `ExecuteNonQueryAsync`,
  `ExecuteScalarAsync<T>`, `ExecuteReaderAsync<T>`

Repositories use the helper; SQL is parameterized at the call site.

## Consequences

**Positive**

- Demonstrates the fundamentals (connection lifecycle, parameterization,
  reader mapping, transactions) that ORMs abstract — fitting for a
  senior-level evaluation.
- The helper centralizes plumbing without becoming a micro-ORM (no SQL
  generation, no entity mapping, no change tracking) — keeps the spirit of
  the constraint.
- Pagination via a single batch (`COUNT(1)` + `OFFSET/FETCH NEXT` in one
  `SqlCommand`, read via `NextResultAsync`) avoids two round trips without
  extra abstraction.

**Negative**

- More boilerplate per repository method than Dapper would produce.
- Risk of someone adding a new repository method that forgets to use the
  helper and writes raw `SqlConnection` code; mitigated by code review and
  the project file reference rule (Application has no `Microsoft.Data.SqlClient`
  reference, so all SQL has to live in Infrastructure).

## Alternatives considered

- **Dapper** — banned by the brief.
- **EF Core** — banned by the brief.
- **RepoDb / SqlKata** — micro-ORMs that would invite the question "if
  you're going to use a micro-ORM, why not Dapper?" The constraint is
  better answered by ADO.NET puro.

## Notes

Real-world choice if the constraint were lifted: Dapper for performance-
sensitive paths, EF Core for richer domains. Here ADO.NET is the
constraint-correct answer.
