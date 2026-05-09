# ADR-006 — Polly retry pipeline as ORM-substitute resilience

**Status:** Accepted (Sprint 1)

## Context

Without an ORM, the project loses EF Core's `EnableRetryOnFailure(...)`
— a default safety net that retries transient `SqlException`s
(timeouts, network blips, transient deadlocks). Production-grade data
access without that net is a backslide; the resilience needs to be
explicit and testable.

## Decision

Wrap every database call in `SqlExecutor` with a **Polly v8
`ResiliencePipeline`**:

- 3 retry attempts
- Exponential backoff (200ms → 400ms → 800ms) with jitter
- Fires on the 18 transient `SqlException` numbers (timeout, network
  reset, deadlock victim, etc.) plus `TimeoutException`

The pipeline is defined once in `SqlExecutor.BuildPipeline(logger)` and
applied uniformly to `OpenConnectionAsync`, `ExecuteNonQueryAsync`,
`ExecuteScalarAsync`, and `ExecuteReaderAsync`. A unit test in
`SqlExecutorPipelineTests` injects a fake operation that throws
transients twice then succeeds — verifies the pipeline retried.

## Consequences

**Positive**

- Restores parity with EF Core's behavior on transient failures.
- Declarative, testable, idiomatic in .NET 10 — Polly v8 is the
  framework Microsoft recommends in
  `Microsoft.Extensions.Http.Resilience`.
- Adding a circuit breaker, timeout, or rate-limiting strategy is a
  one-line addition to the same builder.

**Negative**

- Retry-only protection: a database that's hard down still drains the
  pool while the pipeline retries. Circuit breaker is the obvious
  next step (deliberately out of scope for the take-home but
  trivially added).
- Without an explicit deadline / timeout, a single slow query can
  consume the connection longer than expected. Mitigated by the
  60-second `Connect Timeout` in the connection string.

## Alternatives considered

- **No retry** — fragile against single-packet-loss network blips
  during the panel demo.
- **Manual `try/catch` with `Task.Delay(...)`** — works but reads as
  ad-hoc; doesn't centralize the policy or the metrics.
- **`Microsoft.Extensions.Http.Resilience`** — same Polly under the
  hood, more opinionated for HttpClient. The DB pipeline is direct
  Polly because there's no HttpClient involved.

## Notes

The `IsTransient(SqlException)` helper is `public static` so the
pipeline definition is fully testable without a database. The 18 error
numbers in the catch list are sourced from the well-known list maintained
by the .NET / SQL Server teams.
