# ADR-003 — Argon2id (Konscious) over BCrypt / BCL

**Status:** Accepted (Sprint 1)

## Context

Password hashing is one of the few crypto primitives where the wrong
choice has direct, exploitable consequences (GPU/ASIC-accelerated
brute-force on dumps). The relevant trade-offs are between the algorithm
family (Argon2 / scrypt / bcrypt / PBKDF2) and the supporting library
in .NET (BCL native, Konscious, BCrypt.Net).

## Decision

**Argon2id via `Konscious.Security.Cryptography.Argon2`**, with explicit
OWASP 2024 parameters:

- `DegreeOfParallelism = 1`
- `MemorySize = 19456` KiB (OWASP 2024 minimum)
- `Iterations = 2`
- 16-byte random salt, 32-byte hash output

Format: `argon2id$m=19456$t=2$p=1$<base64-salt>$<base64-hash>` so
verification can parse the parameters and re-derive without external state.

Tuned to ~250ms on the target dev hardware (slow enough to resist brute
force, fast enough to keep login UX responsive).

## Consequences

**Positive**

- Argon2id is the OWASP gold standard since 2015 (winner of the Password
  Hashing Competition). Memory-hard, GPU/ASIC-resistant.
- Parameters are explicit in source, so they can be re-tuned per hardware
  generation without changing the format.
- A guard test `Verify_known_seed_hash_succeeds` holds the literal hash
  used in the seed migration and asserts it still verifies — any future
  format change breaks the test before the demo user breaks in
  production.

**Negative**

- Konscious is a community library; not a Microsoft-owned package. Active
  maintenance and a stable release cadence make this a low risk for the
  exercise scope.
- Memory usage per hash is non-trivial (~19 MiB); the default 1 thread
  keeps the CPU footprint bounded.

## Alternatives considered

- **BCrypt.Net** — widely used but not memory-hard; vulnerable to GPU
  acceleration in 2025+.
- **PBKDF2 (`Rfc2898DeriveBytes`)** — BCL-native but iteration-only
  defense; weaker against modern attacker hardware.
- **BCL Argon2id** — .NET 10 does not ship a public Argon2id API.
  Konscious is the mature, deterministic option.
- **`PasswordHasher<TUser>` from ASP.NET Identity** — uses PBKDF2 by
  default; would have meant adopting the Identity framework just for one
  primitive, which adds surface area not justified for two endpoints.

## Notes

Defense at the panel: "I tuned the work factor consciously to resist GPU
attacks on current hardware" is a stronger answer than "I called
`PasswordHasher`".
