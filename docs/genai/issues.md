# GenAI Audit Trail — Issue Cards

This file is the detailed audit trail referenced from
[`../genai.md`](../genai.md). It is split into two sections that encode
the source of the catch through their structure (not through a `Source`
label):

- **Cleanup-pass cards** — surfaced by Claude reading git diffs at a
  sprint boundary. Five-section template, `### Human revalidation` empty
  by convention. The absence of a human voice in that section IS the
  signal of self-audit origin.
- **Human-discovered cards** — surfaced by the developer through direct
  usage of the running app, manual code review, or live trigger during
  a fix session. Three-section template (no `### Human revalidation`,
  because the entire card is the human's voice). The strongest
  authenticity signal in the audit trail.

Both templates are defined in the project root's
[`CLAUDE.md`](../../CLAUDE.md). Cards are append-only within each
section, chronological by commit; git history at this file confirms
the order. The trailing "caught but not deep-dived" list collects
smaller issues from both sources that didn't warrant a full card.

---

## Cleanup-pass cards (Claude self-audit)

## Issue — `field` keyword traps validation inside Hydrate path

### What AI generated

```csharp
public sealed class Expense
{
    public decimal Amount
    {
        get;
        private set
        {
            if (value <= 0m) throw new DomainValidationException("...");
            field = value; // C# 14 'field' keyword
        }
    }

    public static Expense Hydrate(Guid id, /* ... */ decimal amount, /* ... */)
        => new(id, /* ... */ amount, /* ... */); // ctor → setter → validation
}
```

### Why it's wrong

`Hydrate` exists so repositories can reconstruct entities from DB rows
without re-running invariants the DB-level constraints already guard.
The `field` keyword keeps validation inside the property setter, and the
private constructor goes through the setter, so any edge-case row blows
up during reconstruction. The bypass requirement and the `field` keyword
are mutually exclusive on the same property.

### What was done

```csharp
public decimal Amount { get; private set; }

public static Expense Create(/* ... */)
{
    EnsureValidAmount(amount); // explicit guard before ctor
    EnsureValidDescription(description);
    EnsureValidIncurredAt(incurredAt, utcNow);
    return new Expense(/* ... */);
}

public static Expense Hydrate(/* ... */) => new(/* ... */); // bypasses guards
```

Fix at commit `41bdacd` (Sprint 1.3).

### Human revalidation

---

## Issue — Custom `ApplicationException` shadows `System.ApplicationException`

### What AI generated

```csharp
namespace BallastLane.Application.Common;

public abstract class ApplicationException : Exception
{
    protected ApplicationException(string message) : base(message) { }
}
```

### Why it's wrong

`System.ApplicationException` is in the implicit usings of every .NET
project. Importing `BallastLane.Application.Common` alongside makes
`ApplicationException` ambiguous at every call site that uses both
namespaces — compile error at every throw.

### What was done

```csharp
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}
```

Fix at commit `36a4604` (Sprint 1.4).

### Human revalidation

---

## Issue — `ValidationException` collides with FluentValidation's

### What AI generated

```csharp
using FluentValidation;
using BallastLane.Application.Common;
// ...
throw new ValidationException(errorsDictionary); // CS0104: ambiguous
```

### Why it's wrong

FluentValidation ships its own `FluentValidation.ValidationException`
with a different shape (takes `IEnumerable<ValidationFailure>`); my
domain version takes `IReadOnlyDictionary<string, string[]>`. The
compiler refuses to pick one. Tooling guesses the wrong one in
auto-fix scenarios.

### What was done

```csharp
throw new Common.ValidationException(
    validation.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
```

Fix at commit `36a4604` (Sprint 1.4).

### Human revalidation

---

## Issue — `OptionsBuilder.Bind` does not exist; correct API is `BindConfiguration`

### What AI generated

```csharp
services
    .AddOptions<SqlSettings>()
    .Bind(configuration.GetSection(SqlSettings.SectionName)) // CS1929
    .ValidateDataAnnotations()
    .Validate(s => !string.IsNullOrWhiteSpace(s.ConnectionString), "...");
```

### Why it's wrong

`Bind` is an extension on `IConfiguration`, not on `OptionsBuilder<T>`.
The correct path is `BindConfiguration(sectionPath)` shipped in the
separate package `Microsoft.Extensions.Options.ConfigurationExtensions`.
This is a stale-API failure mode: AI suggests an API that existed in
older library versions but moved.

### What was done

```csharp
// + dotnet add package Microsoft.Extensions.Options.ConfigurationExtensions
services
    .AddOptions<SqlSettings>()
    .BindConfiguration(SqlSettings.SectionName)
    .Validate(s => !string.IsNullOrWhiteSpace(s.ConnectionString), "...")
    .ValidateOnStart();
```

Fix at commit `d7f1f5a` (Sprint 1.5).

### Human revalidation

---

## Issue — Health check stub: filtering ServiceDescriptors does not capture HealthCheckRegistration

### What AI generated

```csharp
ServiceDescriptor[] healthRegistrations = services
    .Where(d => d.ServiceType == typeof(HealthCheckRegistration))
    .ToArray();
foreach (ServiceDescriptor existing in healthRegistrations)
{
    services.Remove(existing); // removes nothing — wrong service type
}
services.AddHealthChecks().AddCheck("stub", () => HealthCheckResult.Healthy(), tags: ["ready"]);
```

### Why it's wrong

`AddHealthChecks().AddCheck(...)` does NOT register `HealthCheckRegistration`
as a service descriptor — it accumulates registrations through
`IConfigureOptions<HealthCheckServiceOptions>`. Filtering DI for
`HealthCheckRegistration` finds nothing, the original `SqlServerHealthCheck`
survives alongside the stub, and `/health/ready` returns 503 in tests
because the stub connection string can't reach SQL Server.

### What was done

```csharp
services.PostConfigure<HealthCheckServiceOptions>(options =>
{
    options.Registrations.Clear();
    options.Registrations.Add(new HealthCheckRegistration(
        name: "sqlserver-stub",
        instance: new StubHealthCheck(),
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready", "db"]));
});
```

Fix at commit `274464e` (Sprint 1.6).

### Human revalidation

---

## Issue — Test JWT secret diverged from the secret the API host actually loaded

### What AI generated

```csharp
// In BallastLaneApiFactory: in-memory override
config.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Jwt:Issuer"] = "BallastLane.Api.Test",
    ["Jwt:Secret"] = "test-only-jwt-secret-must-be-32-chars-or-more-padding-padding",
});

// In StubTokenService: hardcoded matching the in-memory override
public const string Issuer = "BallastLane.Api.Test";
public const string Secret = "test-only-jwt-secret-must-be-32-chars-or-more-padding-padding";
```

### Why it's wrong

`Program.cs` resolves `JwtSettings` directly via
`configuration.GetSection("Jwt").Get<JwtSettings>()` at startup and
feeds those values into `TokenValidationParameters`. WebApplicationFactory
loads `appsettings.Development.json` first; my in-memory override
should win on key collisions, but the JwtBearer pipeline ended up
validating with the dev secret while StubTokenService signed with the
test secret. Cookies were set correctly, then `/auth/me` returned 401
silently — the kind of test failure that wastes an afternoon.

### What was done

```csharp
// StubTokenService now mirrors appsettings.Development.json verbatim
public const string Issuer = "BallastLane.Api";
public const string Audience = "BallastLane.Client";
public const string Secret = "dev-only-jwt-secret-do-not-use-in-prod-XYZ123-padding-padding-padding";

// And the test factory only overrides Sql:ConnectionString — Jwt is
// inherited from appsettings.Development.json so signing and validation
// use the same key by construction.
```

Fix at commit `274464e` (Sprint 1.6).

### Human revalidation

---

## Issue — Validator's `Enum.TryParse` accepted comma-separated values and integer fallbacks

### What AI generated

```csharp
RuleFor(c => c.Category)
    .NotEmpty().WithMessage("Category is required.")
    .Must(value => Enum.TryParse<ExpenseCategory>(value, ignoreCase: true, out _))
        .WithMessage(c => $"Category '{c.Category}' is not recognized. ...");
```

### Why it's wrong

`Enum.TryParse` is leniently spec'd: it trims surrounding whitespace ("food "
parses), accepts comma-separated names regardless of `[Flags]` ("Food,Transport"
parses to a bitwise OR), and accepts ANY integer string ("999" parses to
`(ExpenseCategory)999`). The validator was meant to gate on a known enum
NAME, not pass through anything the parser tolerates. The downstream
`Enum.Parse` in the use case would succeed but produce a value that
round-trips to a non-canonical or out-of-range enum, polluting the
`Category` column and bypassing the SQL CHECK constraint catch-up.

### What was done

Switched to a strict name comparison against `Enum.GetNames<ExpenseCategory>()`
with `OrdinalIgnoreCase`. Only canonical names ("Food", "food", "FOOD") pass;
anything with a separator, padding, or numeric form fails closed.

```csharp
.Must(IsKnownCategoryName)

internal static bool IsKnownCategoryName(string? value)
    => value is not null && Enum.GetNames<ExpenseCategory>()
        .Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase));
```

Fix at commit `0fc4193` (Sprint 2.2).

### Human revalidation

---

## Issue — JWT 'sub' claim was silently remapped to `ClaimTypes.NameIdentifier`

### What AI generated

```csharp
private static Guid ResolveUserId(ClaimsPrincipal user)
{
    string? sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    if (sub is null || !Guid.TryParse(sub, out Guid userId))
        throw new InvalidOperationException("...missing 'sub'...");
    return userId;
}
```

### Why it's wrong

.NET's `JwtSecurityTokenHandler` ships with a non-empty
`DefaultInboundClaimTypeMap` that rewrites `"sub"` to
`ClaimTypes.NameIdentifier` before the principal reaches the endpoint.
Reading only `JwtRegisteredClaimNames.Sub` returns null in production even
though the token explicitly carries the claim. Every authenticated request
to `/expenses` would have thrown the "missing 'sub'" exception — and this
only manifests with the real JwtBearer pipeline, not in unit-tested helpers
that build a `ClaimsPrincipal` directly.

### What was done

Defensive read of both claim names, mirroring the pattern that `/auth/me`
already used in Sprint 1:

```csharp
string? sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
    ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
```

A cleaner long-term fix is `JwtSecurityTokenHandler.DefaultMapInboundClaims = false`
at startup so claims pass through verbatim, but the defensive read keeps both
claim names supported regardless of the global setting and matches the
established `/auth/me` pattern.

Fix at commit `65c9bdc` (Sprint 2.4).

### Human revalidation

---

## Issue — Argon2id seed hash had no first-class generation tool

### What AI generated

The seed migration `0002_seed.sql` needs a literal Argon2id hash for the
demo password. The Argon2id format embeds a random salt, so the hash cannot
be hand-computed — it has to come out of the actual hasher. The first instinct
was to inline a placeholder hash and ask a future contributor to swap it.

### Why it's wrong

A placeholder breaks the round-trip: when grate applies the seed,
`Argon2idPasswordHasher.Verify("Demo@123", placeholder)` returns false and
the demo user can't sign in. The repository had no scaffolded tool to
generate the right hash, so the path of least resistance — copying any
plausible-looking hash literal — would silently break login until somebody
attempted it manually.

### What was done

Two-step approach. First, a temporary xUnit test (`_GenerateSeedHash`) used
`ITestOutputHelper.WriteLine` to print a real hash for `"Demo@123"`; the
value was extracted via
`dotnet test --logger "console;verbosity=detailed" | grep SEED_HASH` and
embedded in the SQL. The temp test was then deleted. Second, a permanent
guard test (`Verify_known_seed_hash_succeeds`) was added to
`Argon2idPasswordHasherTests` that holds the same literal and asserts
`Verify("Demo@123", literal)` returns true — so any future change to the
hash format breaks this test BEFORE the demo user breaks in production.

Fix at commit `2c29247` (Sprint 2.5).

### Human revalidation

---

## Issue — API failed to bind to a known port without a launchSettings.json

### What AI generated

The Sprint 1 API was written assuming the developer would always launch via
`Properties/launchSettings.json` — a file that is gitignored. With a fresh
clone and `dotnet run --no-launch-profile`, Kestrel falls back to
`ASPNETCORE_URLS` (unset), and `appsettings.Development.json` is not loaded
unless `ASPNETCORE_ENVIRONMENT=Development` is also set, so JWT and SQL
config validation throw on startup with cascading inner exceptions.

### Why it's wrong

A new contributor cloning the repo and running `dotnet run` gets two
failures stacked: missing config (ValidateOnStart blows up) AND no
documented URL (the SPA proxy can't target an unknown port). The API
needed a deterministic dev-time binding that worked without per-developer
launch profiles, so `web/proxy.conf.json` and the README could point to
one stable URL.

### What was done

Pinned the Kestrel endpoint in `appsettings.Development.json`:

```json
"Kestrel": {
  "Endpoints": {
    "Http": { "Url": "http://localhost:5080" }
  }
}
```

Now `ASPNETCORE_ENVIRONMENT=Development dotnet run` always binds to 5080,
the SPA proxy targets it deterministically, and the README can document
one URL. The environment-variable requirement remains as a documentation
item for the README in Sprint 3.

Fix at commit `9e8a898` (Sprint 2.6).

### Human revalidation

---

## Issue — Standalone Angular component used a pipe without declaring it in `imports`

### What AI generated

```typescript
@Component({
  selector: 'app-confirm-delete-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule],
  template: `... ({{ data.amount | currency: 'USD' }}) ...`,
})
```

### Why it's wrong

Standalone components in Angular must list every directive AND pipe used
in their template inside the `imports` array — there is no module-level
`BrowserModule.declarations` fallback. Forgetting the pipe yields a
template-compiler error: `NG8004: No pipe found with name 'currency'.`
The build fails, but only when the file is reached during compilation —
easy to miss when iterating on the more visible list / form components.

### What was done

Added `CurrencyPipe` (from `@angular/common`) to the dialog's `imports`
array. Audited the sibling components (`expenses-list`,
`expense-form.dialog`); both already imported `CurrencyPipe` and `DatePipe`
correctly from the start, so the issue was localized to the late-added
confirmation dialog.

Fix at commit `c4dcc3d` (Sprint 2.7).

### Human revalidation

---

## Human-discovered cards (usage + direct audit)

## Issue — SqlClient `localhost` resolves to IPv6 against Podman-hosted SQL Server

### What I observed

Running `dotnet run --project db/BallastLane.Migrations` against the
Podman-hosted SQL Server hung for 30 seconds and then died with the
misleading error "the wait operation timed out". The container itself
was healthy; `podman exec ... sqlcmd` against the same instance
returned `1` immediately. The hang only happened from the .NET side.

### Why it's wrong

Microsoft.Data.SqlClient 7.0 + Podman's port forwarding triggers an
IPv6 resolution path that times out before falling back to IPv4. The
generated connection string used `Server=localhost,1433`, which maps
to `::1` first; the listener is published only on the IPv4 bridge.
Each grate run paid the full TCP timeout on the IPv6 attempt before
failing closed, with no useful error pointing at the resolution path.

### What was done

```csharp
const string DefaultConnectionString =
    "Server=tcp:127.0.0.1,1433;Database=BallastLane;User Id=sa;Password=BallastLane@2026!;TrustServerCertificate=True;Encrypt=True;Connect Timeout=60";
```

The `tcp:127.0.0.1` literal forces the IPv4 path; `Connect Timeout=60`
gives migrations a longer ceiling for cold container starts. Fix at
commit `5ce380c` (Sprint 1.2). Sprint-boundary review: existing fix
sized appropriately for a take-home; considered hardening (remove
hardcoded `sa` credentials, fail-loud on missing config) but rejected
as over-engineering for a 7-day exercise where dev fixtures are not
real secrets.

---

## Issue — Amount input retains '0' default, forcing manual deletion before typing

### What I observed

Opening the New Expense dialog, the Amount field is pre-filled with the
literal value `0`. Typing a real amount (e.g. `12.50`) appends to the
default value rather than replacing it, producing `012.50`. The
workaround on every create is to select-all + delete (or just the
leading `0`) before typing — a friction point on what is supposed to
be the most-used flow in the app.

### Why it's wrong

The reactive form was initialized with
`amount: [0, [Validators.required, Validators.min(0.01)]]`. The
validators are correct (required + > 0.01), but the initial value of
`0` creates a UX trap: the field LOOKS like it carries a placeholder,
but it actually holds a real value, and `<input type="number">` does
not auto-select on focus. Standard form UX for numeric inputs is to
start empty (showing a placeholder hint) or to auto-select on focus —
never to sit on a literal zero that requires manual deletion.

### What was done

Switched the amount control to a typed `FormControl<number | null>`
with `null` as the create-time initial value (the edit path still
hydrates from the existing expense). Added `placeholder="0.00"` and
`inputmode="decimal"` to the input so the field reads as empty with
a format hint on create:

```typescript
amount: new FormControl<number | null>(
  this.data.expense?.amount ?? null,
  { validators: [Validators.required, Validators.min(0.01)] },
),
```

Submit handler narrows `value.amount` from `number | null` to `number`
via an explicit guard — unreachable in practice because
`Validators.required` would have already invalidated the form, but
kept for type narrowing and defensive clarity. Considered also
adding `(focus)="select()"` to help the edit flow but rejected as
out of scope: the reported bug is the create-time `0`, and Material's
default is no auto-select. Fix at commit `60c5b1d`.

---

## Issue — Form fields surface validation error on blur without any input

### What I observed

Clicking into the Amount input on the New Expense dialog and clicking
out again without typing immediately turns the field red and shows
"Amount must be greater than zero." Same behavior on every other
required field across the app — the email and password fields on the
login screen, the register screen, and Category / Date on the expense
form all flash a red error the instant the user tabs through them.
The user hasn't entered anything wrong yet; they just touched the
field. The accusatory red feels disproportionate.

### Why it's wrong

The `mat-error` blocks were written as
`@if (form.controls.X.invalid && form.controls.X.touched)`, and
Angular Material's default `ErrorStateMatcher` follows the same
`touched && invalid` rule. Required fields are invalid by default
(until the user types), so any focus-then-blur trips error visibility
immediately. Standard form UX surfaces errors only when (a) the user
has actually typed something the validator rejects (`dirty && invalid`),
or (b) the user attempted to submit (`submitted && invalid`) — never on
a passive blur of a still-empty field. Material already ships a matcher
for exactly this rule (`ShowOnDirtyErrorStateMatcher`), but the default
DI wiring doesn't use it.

### What was done

Provided `ShowOnDirtyErrorStateMatcher` globally in `app.config.ts` so
all three forms (login, register, expense create/edit) inherit the
new visibility rule with one line:

```typescript
import { ErrorStateMatcher, ShowOnDirtyErrorStateMatcher } from '@angular/material/core';
// ...
{ provide: ErrorStateMatcher, useClass: ShowOnDirtyErrorStateMatcher }
```

Errors now surface when the user has typed something invalid OR
clicked the submit button — not on a blur of a still-empty field. The
component-local `@if (invalid && touched)` guards still gate whether
the `<mat-error>` element is projected into the DOM, but Material's
form-field gates final visibility on the matcher's `errorState`; the
guards are now redundant but harmless and were left in place to avoid
a refactor that wasn't on the bug. Fix at commit `996a489`.

---

## Issue — `0.00` format placeholder hidden by Material's label-inside default

### What I observed

After landing the leading-zero fix (card above), the Amount input on
the New Expense dialog reads as empty — but the `placeholder="0.00"`
that was supposed to hint at the format is nowhere to be seen. Instead,
the floating label `Amount*` sits inside the input area when the field
is empty + unfocused, occupying the slot where the placeholder would
go. Click in, click out without typing → the label drops back inside
and the format hint stays invisible.

### Why it's wrong

Material's `appearance="outline"` form-field has two label states: the
label "floats" up to the border when the input is focused or has a
value, and "drops" back inside the input when both empty and unfocused.
When the label is inside, Material intentionally hides any HTML
`placeholder` to avoid two pieces of text overlapping. Result: the
`placeholder="0.00"` added in `60c5b1d` is dead code in the most
common state (empty New Expense dialog just opened), defeating the
intent of giving the user a format hint.

### What was done

Set `floatLabel="always"` on the Amount `<mat-form-field>` so the
label stays pinned to the top border in every state. With the label
permanently floated, Material renders the placeholder inside the
input area, and `0.00` is visible from the moment the dialog opens
through every focus/blur cycle until the user types a value.

```html
<mat-form-field appearance="outline" floatLabel="always" class="full">
  <mat-label>Amount</mat-label>
  <input matInput type="number" placeholder="0.00" ... />
</mat-form-field>
```

Scoped to the Amount field only. Category, Date, and Description
don't have placeholders to surface, so leaving them with the
default `floatLabel="auto"` keeps the cleaner Material look. Fix
at commit `996a489`.

---

## Issue — Amount input lacks currency-grade entry semantics

### What I observed

The Amount input was `<input type="number">`, which delivered three
separate UX gaps for a USD-only expense field:

1. The decimal separator follows the browser locale — values typed
   from a locale that uses `,` as the decimal separator wouldn't
   round-trip cleanly against an API and UI that format USD as `12.34`
   everywhere else.
2. Two decimal places weren't enforced — the browser accepted
   `12.345`, leaving the server validator as the only line of defense.
3. To enter `$12.34` the user had to type whole digits, then `.`,
   then cents — a two-step decimal entry for what is conceptually a
   single currency value. Cash-register-style entry (digits build
   the value from the cents side) is the convention for currency
   inputs and is what the user expected.

### Why it's wrong

For a USD-only expense tracker, the Amount input shape should be
fixed: period as decimal separator, exactly two decimal places, and
cash-register entry from cents. A locale-dependent `<input type="number">`
delivers none of these guarantees.

### What was done

Switched the Amount input to `<input type="text" inputmode="decimal">`
managed manually instead of via `formControlName`, so the dialog
component owns the entry pipeline:

- **Display**: `amountDisplay()` returns `value.toFixed(2)` whenever
  the value is non-null — period separator, two decimals, no locale
  leakage.
- **Typing**: each keystroke is intercepted; digits APPEND to the
  cents side of the value (`1`, `2`, `3`, `4` → `0.01`, `0.12`,
  `1.23`, `12.34`); Backspace and Delete remove the last cent.
  Capped at `$99,999,999.99` (`9_999_999_999` cents) to fit
  `DECIMAL(18,2)` with margin and to bound integer math.
- **Paste**: pasted text is parsed as a regular decimal (not
  cash-register), so `12.50`, `12,50`, `$12.50`, and `1,234.50` all
  resolve to the right cents. Garbage that contains no digits is
  silently dropped.
- **Blur**: explicitly marks the form control as `touched`, which
  `formControlName` would have wired automatically — needed so
  `mat-error` and the `ShowOnDirtyErrorStateMatcher` from the
  previous card see the right state.

The reactive form keeps `amount` as a `FormControl<number | null>`
with the existing `Validators.required` and `Validators.min(0.01)`;
the component just owns the template-side wiring now. Edit-flow
choice (per user direction): keystrokes append to the existing
value rather than reset on focus — if the user wants to retype
from scratch they backspace the old value out first. Fix at
commit `996a489`.

---

## Issue — Email validator accepted addresses without a TLD (e.g., `user@host`)

### What I observed

Typing `user@example` (no `.com` / `.test` / TLD of any kind) into the
Email field of the Login or Register dialog does not trigger any
client-side error. The form passes validation and the request hits the
server, which on Login returns a generic 401 ("Invalid email or
password.") that doesn't tell the user which side is wrong — typo in
the email or wrong password? On Register the surface is similar: the
client says nothing, the server eventually rejects, and the user is
left guessing whether the form is broken or their input is bad.

### Why it's wrong

`Validators.email` follows the HTML5 spec, which treats `user@host`
(local-only domain) as a valid address — historically because email is
used inside private networks too. For a public web app where users
expect to type their real email, the HTML5 permissive shape is wrong:
the user's mental model is `name@domain.tld`, and any address that
visibly lacks a TLD should fail closed at the client before a round
trip to the server.

### What was done

Added `Validators.pattern(/^[^\s@]+@[^\s@]+\.[^\s@]+$/)` to the email
control on both `LoginComponent` and `RegisterComponent`. The pattern
requires at minimum `local@domain.tld` — one or more non-space,
non-`@` characters before the `@`, after the `@`, and after the
mandatory `.`. `Validators.email` is kept so the existing length and
character checks still run; the pattern is added on top as a stricter
gate. The mat-error message ("Please enter a valid email address.")
covers both validators since the user-visible meaning is the same.
Server-side `FluentValidation`'s `EmailAddress()` was left as-is —
client tightening is enough to remove the round-trip on obvious typos,
and the server still has the final word. Fix at commit `996a489`.

---

## Issue — Form-field errors still surfaced during typing under ShowOnDirty

### What I observed

After `996a489` swapped the default `ErrorStateMatcher` for
`ShowOnDirtyErrorStateMatcher`, the focus-then-blur flash was gone but
errors still appeared LIVE as the user typed — for example, typing
`abc` into the Email field on the Login screen surfaced "Please enter
a valid email address." mid-keystroke. Same on Register and on the
Amount field of the New Expense dialog. The user wanted validation
to be a clean SUBMIT step: type freely without interruption, click
"Sign in" / "Create account" / "Create" / "Save", THEN see all errors
together. Live error feedback during typing still dirties the visual
experience.

### Why it's wrong

`ShowOnDirtyErrorStateMatcher` returns true on
`(dirty || submitted) && invalid`. Any keystroke marks the control
dirty, so the moment the user types something that fails a validator
(an in-progress email, a partial password) the error surfaces. For
this app's preferred UX, validation visibility should be deferred
completely until the user expresses intent to submit. The HTML5 /
Material default was too eager; `ShowOnDirty` was a half-measure.

### What was done

Replaced the global `ShowOnDirtyErrorStateMatcher` provider with a
custom `SubmittedErrorStateMatcher` (`core/error-state-matcher.ts`)
that returns `true` only when `(submitted && invalid)`:

```typescript
@Injectable({ providedIn: 'root' })
export class SubmittedErrorStateMatcher implements ErrorStateMatcher {
  isErrorState(
    control: FormControl | null,
    form: FormGroupDirective | NgForm | null,
  ): boolean {
    return !!(control && control.invalid && form && form.submitted);
  }
}
```

`FormGroupDirective.submitted` flips to `true` on the form's first
`(ngSubmit)`, so until then no error visibility on any field. After
the first submit, errors become live so the user sees corrections
update as they type. Applies globally to login, register, and the
expense form. The `markAllAsTouched()` calls in submit handlers are
left in place — harmless under the new matcher and still useful if a
field-level consumer ever depends on `touched`. Fix at commit
`8a9e23e`.

---

## Issue — `ExpenseFormDialog` could send `incurredAt` ahead of the server's clock

### What I observed

The deferred bullet in the prior cleanup pass flagged this as a
theoretical gap, but the failure mode is real: the `MatDatepicker`
hands back a JavaScript `Date` representing midnight in the user's
LOCAL timezone, and `value.incurredAt.toISOString()` mechanically
converts that to UTC. For users in UTC-positive timezones who pick
"today" early in their local day, the resulting wire value can be
AHEAD of the server's `DateTime.UtcNow` — the server's domain
validator (`EnsureValidIncurredAt`) then rejects with
"IncurredAt cannot be in the future." and the user gets a 400 they
have no obvious way to interpret.

A second, quieter failure mode: even when the value is in the past
from the server's view, the calendar day stored may not match the
calendar day the user picked when later displayed in a different
timezone (silent date-shift on read).

### Why it's wrong

The picker's `[max]="today"` doesn't help — `today` is a `Date` in
the user's local timezone, so the picker's notion of "today" can be a
day ahead of the server's UTC `today`. The straight `.toISOString()`
conversion preserves the LOCAL midnight as a UTC instant, which is
the wrong semantic: the user picked "this calendar day", not "midnight
of this day in my timezone". The two interpretations only collide for
users in non-zero offsets, but that's most of the world.

### What was done

Added a `toIncurredAtUtcIso(picked: Date)` static helper on
`ExpenseFormDialogComponent` that branches on the picker's
relationship to "today":

- **Today (in local timezone)** → send `new Date().toISOString()`
  (the current UTC moment). By construction the value is never ahead
  of the server's `DateTime.UtcNow`, so the future-date rejection
  cannot trigger.
- **Past date** → send noon UTC of the selected day
  (`Date.UTC(y, m, d, 12, 0, 0)`). Noon UTC stays on the same calendar
  day when displayed in any timezone from UTC-12 through UTC+11 —
  covers every practical user locale for this take-home with a small
  buffer on each side. Users in UTC+12 / +13 (Tonga, Samoa, Fiji)
  may see a +1 day shift on display of past dates; documented as a
  known edge.

Replaced the bare `value.incurredAt.toISOString()` in `onSubmit`
with a call to the helper. Localized to one file; no server change.
The deferred Sprint 2.1 domain UTC + Category guards work would
harden this further on the server side but is out of scope for this
card. Fix at commit `(pending)`.

---

## Issues caught but not deep-dived

> One-liners for issues that were caught and fixed but didn't warrant a full
> Issue Card. Each ties to a real commit (or is flagged as a deferred gap).

- `dotnet new` templates for .NET 10 still emit `Class1.cs` / `UnitTest1.cs` boilerplate — deleted in initial commit (`3bb1d88`)
- `grate --databasename` flag does not exist; database is encoded in the connection string (`5ce380c`)
- `AppException` was originally `sealed`, which prevented `ConflictException` from inheriting; relaxed to non-sealed (`36a4604`)
- `HealthCheckWriters` placed in `BallastLane.Api` namespace but `Program.cs` (top-level statements) had no matching `using` — added missing import (`274464e`)
- First Domain test file omitted `using BallastLane.Domain.Common`, even though `DomainValidationException` was thrown by `User.Create` — added import (`41bdacd`)
- `ng add @angular/material` did not pull in `@angular/animations` as a peer; manual `npm install @angular/animations@^21.2.0` was required before the dev build resolved (`9e8a898`)
- NG8107 warning: `form.controls.description.value?.length` used `?.` on a non-nullable form value; collapsed to plain `.value.length` (`c4dcc3d`)
- `provideZonelessChangeDetection` was added then removed during Sprint 2.6 to stay aligned with the original "zone-based" plan; OnPush + signals were kept since they work in both modes (`9e8a898`)
- `provideAppInitializer` blocks app boot until `auth.refresh()` resolves; if `/auth/me` hangs because the API is unreachable, the SPA sits on a white screen with no fallback timeout — gap, deferred (`9e8a898`)
- API antiforgery middleware (`UseAntiforgery`) is still not wired; CSRF cookies are issued but never validated on `POST /expenses`, `PUT`, or `DELETE` — Sprint 1 carry-over, deliberately consistent
