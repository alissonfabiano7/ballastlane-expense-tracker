# ADR-005 — Minimal APIs with `MapGroup` over Controllers

**Status:** Accepted (Sprint 1)

## Context

ASP.NET Core .NET 10 supports both classic Controllers and Minimal APIs.
For a small CRUD with two feature areas (auth + expenses), the choice
between them shapes how endpoints, validators, and middleware compose.

## Decision

Use **Minimal APIs organized by feature** via `MapGroup`:

- `AuthEndpoints.MapGroup("/auth").MapPost(...)` for the auth surface
- `ExpensesEndpoints.MapGroup("/expenses").RequireAuthorization().RequireAntiforgery().MapPost(...)` for the CRUD surface

Cross-cutting concerns (validation, antiforgery) attach via
`IEndpointFilter` and route group conventions
(`RequireAuthorization()`, the custom `RequireAntiforgery()`,
`.DisableAntiforgery()`).

## Consequences

**Positive**

- Less reflection at startup, smaller cold start.
- Each endpoint group is one file; the structure scales by adding files,
  not by adding controllers + DTOs + filters in three different
  directories.
- `MapGroup` gives the same separation as Controllers without the MVC
  filter pipeline — `IEndpointFilter` covers what MVC filters used to.
- Fits the existing use-case-per-class pattern naturally: an endpoint
  is a 2-line shim that injects a use case and calls `HandleAsync`.

**Negative**

- The convention "all endpoints in one file per feature" is enforced by
  discipline, not by the framework. Code review must keep
  `Program.cs` from accumulating inline `MapPost` calls.
- Less mature OpenAPI tooling than the Controllers world (improving
  every release).
- Some MVC concepts (`[FromForm]`, model binding metadata) require
  explicit opt-in or work around via filters — surfaced when wiring
  anti-forgery for JSON endpoints (the `IAntiforgeryMetadata`
  interface didn't reliably trigger validation; switched to an
  explicit endpoint filter).

## Alternatives considered

- **Classic Controllers** — fine but reads as "legacy default" in
  .NET 10. Would have added MVC pipeline overhead for no win at this
  scale.
- **Carter / FastEndpoints** — opinionated frameworks on top of Minimal
  APIs. The framework here is small enough that adding a
  third-party layer would invite "why?" without paying for itself.

## Notes

The CSRF-enforcement journey (documented in a Human-discovered card)
exposed one rough edge: `app.UseAntiforgery()` doesn't validate JSON
endpoints just by adding `IAntiforgeryMetadata` — a dedicated endpoint
filter is the reliable path. This is a Minimal API quirk, not an
indictment.
