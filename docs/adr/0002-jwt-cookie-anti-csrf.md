# ADR-002 — JWT in HttpOnly cookie + anti-CSRF (no `localStorage`)

**Status:** Accepted (Sprint 1)

## Context

The auth surface is JWT-based (HS256, 1h TTL). The remaining choice is how
to deliver and store the token on the client: classic
`Authorization: Bearer ...` from `localStorage`, or an HttpOnly cookie
managed by the browser. Each carries a different risk profile.

## Decision

**JWT in `HttpOnly` + `Secure` + `SameSite=Lax` cookie**, paired with
**anti-CSRF tokens** via `Microsoft.AspNetCore.Antiforgery`.

- The auth cookie (`ballastlane.auth`) is `HttpOnly`, so the SPA's
  JavaScript never reads it; an XSS-injected script cannot exfiltrate the
  token.
- `JwtBearer` is configured with an `OnMessageReceived` event that lifts
  the token out of the cookie before the standard `Authorization` header
  inspection.
- A second cookie `XSRF-TOKEN` (non-HttpOnly) carries the antiforgery
  request token; the SPA reads it via JS and sets the `X-XSRF-TOKEN`
  header on every mutation.
- A small `RequireAntiforgery()` extension adds an `IEndpointFilter` that
  calls `IAntiforgery.ValidateRequestAsync` on every
  `POST/PUT/PATCH/DELETE` under `/expenses` and on `POST /auth/logout`.
  `/auth/login` and `/auth/register` opt out via `.DisableAntiforgery()`
  (anonymous bootstraps; the user has no token before authenticating).

## Consequences

**Positive**

- XSS defense in depth: even with a hostile script in the page, the auth
  token cannot leave the browser via JS.
- `SameSite=Lax` blocks the bulk of cross-site CSRF automatically; the
  antiforgery token covers the rest.
- The contract with the SPA stays simple: no token plumbing in
  application code, just `withCredentials: true` and one interceptor.

**Negative**

- Two-step bootstrap for the antiforgery flow: the login-time tokens are
  bound to the still-anonymous principal, so the SPA has to call
  `/auth/csrf-token` after login to re-bind them. Documented in a
  Human-discovered card and in the test fixture.
- Cookie-based auth doesn't generalize cleanly to native mobile clients
  (no `cookieStore`); for a future mobile target, a Bearer fallback
  endpoint would be added.

## Alternatives considered

- **`Authorization: Bearer ...` from `localStorage`** — simplest to wire
  but XSS-vulnerable by design. A senior-level take-home in 2026 doesn't
  defend it.
- **BFF (Backend-for-Frontend) pattern** with an opaque session cookie
  and the JWT held server-side — strictly more secure (no token reaches
  the browser at all), but a 6-8h refactor and out of scope for the 72h
  envelope.

## Notes

In production I'd add refresh tokens with rotation + family detection;
deliberately out of scope for the take-home.
