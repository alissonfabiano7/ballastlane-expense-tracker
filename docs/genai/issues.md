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
default is no auto-select. Fix at commit `(pending)`.

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
- `ExpenseFormDialog` serializes the date picker's `Date` via `toISOString()`; a user in UTC-N selecting "today" near local midnight can produce an `incurredAt` the server rejects as "in the future" — gap, deferred together with the Sprint 2.1 domain UTC work (`c4dcc3d`)
- API antiforgery middleware (`UseAntiforgery`) is still not wired; CSRF cookies are issued but never validated on `POST /expenses`, `PUT`, or `DELETE` — Sprint 1 carry-over, deliberately consistent
