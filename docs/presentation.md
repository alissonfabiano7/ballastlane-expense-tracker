# Presentation Script — Ballast Lane Take-Home

> **Format.** Markdown projected in Zoom alongside a live editor and the
> running app. Target 12 min, with a 15-min ceiling for flex. This is a
> *presenter's script* — what to say, where to click, what to point at —
> not slides.
>
> **Pre-flight.** Have the API + Angular dev server already running in
> two terminals before the call starts; have two browser tabs open
> (one logged out at `localhost:4200`, one logged in showing the
> expense list); have IDE open with the solution. Do NOT start
> `dotnet run` or `ng serve` on screen — bootstrap delays burn time.

---

## 0:00 – 1:30 · Opening + user story

> "Hi, I'm Alisson. I built a Personal Expense Tracker for the Ballast
> Lane Senior .NET take-home. The user story is simple — `as a person
> who cares about personal finances, I want to record and categorize
> my expenses so I can see where my money is going` — chosen
> deliberately because it's a domain anyone understands in 30 seconds,
> with real business rules (resource-level ownership, validation,
> pagination) but bounded enough that I could ship it well in 28
> productive hours. The full plan, the architecture document, eight
> ADRs, the audit trail of corrections during development, and a
> live demo all sit in this repo."

*(Show README.md in the editor, scroll once to give a sense of
structure — Quick Start, security baseline, ADR index, GenAI link.)*

---

## 1:30 – 4:00 · Live demo

*(Switch to the logged-out browser tab.)*

> "Let me sign in as the demo user."

- Type `demo@ballastlane.test` / `Demo@123`.
- After landing on the expenses list:
> "Eight seed expenses across all categories. Material card layout,
> responsive grid, paginator at the bottom, color-coded category chips.
> Let me create a new expense."

- Click "New expense":
> "Notice the form — Amount field already shows `$ 0.00` as a
> placeholder, the label stays floated above. The input is a custom
> cash-register entry pattern: I type `1234` and you'll see it build
> from the cents side."

- Type `1234`, narrate `0.01 → 0.12 → 1.23 → 12.34`.

> "Picks period as the decimal separator regardless of browser locale
> — relevant for a USD-only field. Backspace removes the last cent."

- Hit backspace twice → `0.12`.
- Pick a category, pick a date.
- Click Create:
> "Created. Snackbar feedback. The new expense shows up in the list
> ordered by date."

*(Click the menu on the new expense → Edit.)*

> "Edit — same form, pre-filled. Save."

*(Edit, save, snackbar.)*

> "Delete — confirmation dialog, irreversible action gets a gate."

*(Delete, confirm, snackbar.)*

*(Click Sign out.)*

> "Logout. The auth cookie is cleared server-side."

> "Two negative paths worth showing — bad credentials and validation."

- Try login with `demo@ballastlane.test` / wrong password → 401, error
  banner.
- Type just an email with no `@` — show the form error appears only
  *after* clicking Sign in (deferred error visibility — explained in
  the technical walk-through next).

---

## 4:00 – 8:00 · Architecture walk-through

*(Switch to IDE. Show the solution tree.)*

> "Four projects, classic Clean Architecture: Domain, Application,
> Infrastructure, Api. The dependency rule is enforced by project file
> references — Application has no `Microsoft.Data.SqlClient` reference,
> so SQL literally cannot leak in. Domain has no dependencies at all."

*(Open `Application/Expenses/CreateExpenseUseCase.cs`.)*

> "Use case is one class with one `HandleAsync` method. Validates the
> command via FluentValidation, calls the domain factory `Expense.Create`
> which enforces invariants in the constructor, persists via the
> repository interface. The `userId` is a required parameter — every
> repo method that touches a user-owned resource takes it. There is no
> `GetById(id)` overload, only `GetById(id, userId)`. Resource-level
> authorization is impossible to violate by accident."

*(Open the corresponding test
`Application.Tests/Expenses/CreateExpenseUseCaseTests.cs`.)*

> "TDD-driven. NSubstitute for the repository fake, Shouldly for
> assertions — Shouldly because FluentAssertions went commercial in
> January 2025 and I didn't want a license footgun in the deliverable.
> The test file pairs one-to-one with the use case file."

*(Open `Api/Expenses/ExpensesEndpoints.cs`.)*

> "Endpoints are Minimal APIs organized by feature with `MapGroup`.
> Each endpoint is a 3-line shim: takes the use case via DI, calls
> `HandleAsync`, returns a typed result. The group has
> `RequireAuthorization()` and a custom `RequireAntiforgery()`
> extension that wires anti-CSRF validation on every mutating method —
> I'll come back to that in the technical choices section."

*(Open `Infrastructure/Persistence/SqlExecutor.cs`.)*

> "ADO.NET puro by constraint of the brief, but wrapped in a 140-line
> helper. Polly v8 ResiliencePipeline gives me retry-with-jitter on
> transient SqlException — this is my replacement for what EF Core's
> `EnableRetryOnFailure` would have given me. Three retries,
> exponential backoff, eighteen well-known transient error numbers.
> Tested against a fake injected operation."

*(Open `Api/Program.cs`, scroll the pipeline.)*

> "Composition root. Pipeline is in deliberate order — security headers
> first, then exception handling, then CORS as a named policy with
> `AllowCredentials` (never `AllowAnyOrigin`), then auth, authz,
> antiforgery, then endpoints. The JWT is in an HttpOnly cookie —
> JavaScript never touches it — and cookie extraction wires into
> `JwtBearer` via the `OnMessageReceived` event."

---

## 8:00 – 10:00 · Notable technical choices

> "A few decisions worth defending. Each has a one-page ADR in
> `docs/adr/`."

**1. ADO.NET puro over micro-ORMs.** *(20s)*

> "Banned by the brief; even unconstrained, ADO.NET demonstrates the
> fundamentals an ORM hides. The `SqlExecutor` helper centralizes
> plumbing without becoming a micro-ORM."

**2. JWT in HttpOnly cookie + anti-CSRF, NOT localStorage.** *(20s)*

> "XSS defense in depth. Even with a hostile script in the page, the
> token can't leave the browser via JS. SameSite=Lax handles most
> cross-site CSRF; the antiforgery token covers the rest. Wiring
> antiforgery on JSON endpoints in .NET 10 had a footgun — the
> framework's metadata path didn't trigger validation reliably, so I
> dropped to an explicit `IEndpointFilter` calling
> `IAntiforgery.ValidateRequestAsync`. That story is in card 8 of the
> audit trail."

**3. Argon2id (Konscious) with OWASP 2024 parameters.** *(20s)*

> "OWASP gold standard since 2015 — memory-hard, GPU-resistant.
> Parameters tuned to ~250ms on target hardware. The literal seed hash
> in `0002_seed.sql` is guarded by a unit test so a future format
> change can't silently break the demo user."

**4. Minimal APIs with `MapGroup` over Controllers.** *(15s)*

> "Default in .NET 10. `MapGroup` per feature gives the same
> separation as Controllers without the MVC pipeline reflection."

**5. Polly v8 retry pipeline as ORM-substitute resilience.** *(15s)*

> "Without an ORM, no `EnableRetryOnFailure`. Polly's
> `ResiliencePipeline` is the declarative substitute and tested against
> a fake transient operation."

**6. Conventional Commits in English imperative.** *(15s)*

> "git log reads as a TDD storyline: `test:` then `feat:` then
> `refactor:`. Linear branch, no squash, every commit is a human gate
> per `CLAUDE.md` — even the AI pair never commits autonomously."

**7. Angular 21 zone-based + Reactive Forms over 22 + Signal Forms.**
*(20s)*

> "Original plan targeted 22 + Signal Forms; 22 was still pre-release
> at scaffold time. Pinned to 21 stable. Tried zoneless briefly —
> Material's CDK overlays were unstable — reverted to zone-based for
> demo predictability. Honest plan-vs-shipped delta documented in ADR
> 4 and the audit trail."

---

## 10:00 – 11:30 · GenAI

*(Open `docs/genai.md`.)*

> "The GenAI section is the headline; the audit trail is in
> `genai/issues.md` — append-only, organized into two sections by catch
> source: cleanup-pass cards (Claude self-audit at sprint boundaries,
> empty `Human revalidation` by convention) and human-discovered cards
> (issues I caught by running the app or reading the code, written
> entirely in my voice). The split is structural — the section the card
> lives in IS the catch source. There is no `Source` label that could
> be fabricated after the fact."

*(Open `docs/genai/issues.md`, scroll to the human-discovered section.)*

> "Eight human-discovered cards. The leading-zero default in the
> Amount input. The premature error timing on form blur. The hidden
> placeholder when Material's label was inside the field. The
> cash-register entry I just demoed. The locale-dependent decimal
> separator. The email validator accepting `user@host` without a TLD.
> The timezone shift in `incurredAt`. And the unenforced CSRF on
> mutating endpoints — that one was a real security gap that the
> Sprint 1 cleanup pass missed and I caught by reviewing the
> middleware order."

*(Open `docs/genai.md` section 5 — final reflection.)*

> "Two patterns from the audit trail. What Claude got consistently
> wrong: stale APIs — suggesting `OptionsBuilder.Bind` instead of
> `BindConfiguration`, treating `HealthCheckRegistration` as a service
> descriptor, assuming `AntiforgeryMetadata.ValidationRequired` is
> public when it's internal. What it got surprisingly right: the
> boring scaffolding — project structure, exception-to-ProblemDetails
> mapping, test fixtures. Strong on the well-documented patterns; weak
> on framework-version-specific APIs."

> "The strongest authenticity signal in this deliverable is that the
> human-discovered section is non-empty. Proof that I exercised the
> running app and caught what the AI's review missed."

---

## 11:30 – 12:00 · Trade-offs and what I'd do differently

> "What's deliberately not here, in priority order if there were a
> Sprint 4: refresh tokens with rotation. A circuit breaker on the
> Polly pipeline — I have retry, no breaker. CI via GitHub Actions —
> the test suite is structured so it's a one-file workflow.
> TestContainers integration tests against a real SQL Server — the
> project is scaffolded, no tests yet. ArchUnitNET rules to assert the
> dependency model as code. Rate limiting on `/auth/login`. Full CSP
> instead of `default-src 'self'`. Frontend tests with Karma or
> Jasmine."

> "One known limitation worth flagging: the `provideAppInitializer`
> call in the Angular bootstrap blocks app boot until `/auth/me`
> resolves. If the API is unreachable, the SPA sits on a white screen
> with no fallback timeout. Documented as a known limitation in the
> README. The fix is a five-line RxJS `timeout(5000)`; deliberately
> deferred."

> "Happy to take questions."

---

## Q&A — answers I have ready

> *(These are NOT presented; they're the bench. The full Q&A bank
> from the planning artifact lives in
> [`genai/planning-session.md`](./genai/planning-session.md).)*

- **"Why not EF/Dapper in production?"** → ADR-001. In production:
  Dapper for perf-sensitive paths, EF Core for richer domains.
- **"How do you do transactions without an ORM?"** →
  `SqlConnection.BeginTransaction()` explicit. Encapsulated in an
  `IUnitOfWork` if needed; use case opens, repos receive the active
  connection via DI scoped per request, middleware handles rollback.
- **"What did you correct most in AI output?"** → Stale APIs (cards
  5 and 6 are good examples). Resource-level authorization is the
  classic failure for AI-generated CRUD; here the repo signature
  forces it.
- **"Why .NET 10 and not 8 LTS?"** → ADR doc covers it. .NET 8 LTS
  expires Nov/2026 (~6 months runway); .NET 10 is the current LTS
  through Nov/2028.
- **"Why Shouldly and not FluentAssertions?"** → FA v8 went
  commercial January 2025. Shouldly is the MIT alternative; sintaxe
  is `result.ShouldBe(...)` vs `result.Should().Be(...)`.

---

## Demo prep checklist

- [ ] API running on `:5080` (`dotnet run --project src/BallastLane.Api`)
- [ ] Angular dev server on `:4200` (`cd web && npm start`)
- [ ] Database container up (`podman ps` shows `ballastlane-sqlserver`)
- [ ] Two browser tabs ready: one logged out, one logged in
- [ ] IDE open with `Application/Expenses/CreateExpenseUseCase.cs` and
      its test in adjacent tabs
- [ ] `Program.cs` already scrolled to the pipeline section
- [ ] `docs/genai.md` and `docs/genai/issues.md` open in editor for
      the GenAI section
- [ ] Cronometre uma vez antes da chamada
