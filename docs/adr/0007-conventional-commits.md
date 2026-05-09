# ADR-007 — Conventional Commits in English imperative

**Status:** Accepted (Sprint 1)

## Context

The commit history is a deliverable for this take-home — the panel will
open `git log` and read the rhythm of work. Commit message conventions
need to be decided up front so the log communicates seniority rather than
documenting it after the fact.

## Decision

**Conventional Commits** (`feat:`, `fix:`, `test:`, `refactor:`,
`docs:`, `chore:`, `perf:`, `build:`, `ci:`) in **English imperative
mood**.

- Imperative present, not past or gerund: `add tests`, never
  `added tests` or `adding tests`.
- One commit per logical change; small and atomic.
- Linear `main` branch — no squash, no merge commits.
- Backfill commit hashes in audit-trail cards as a second commit
  immediately after the fix lands (the
  `(pending)` → `<hash>` rotation).

## Consequences

**Positive**

- `git log --oneline` reads as a TDD storyline:
  `test: cover X` → `feat: implement X` → `refactor: ...`. The panel
  doesn't have to ask "did you actually do TDD?".
- Allows automated changelog generation if the project ever grows
  beyond a take-home.
- English imperative is the open-source default and the consensus in
  international teams; aligns the artifact with what a panel from any
  region expects.
- Scoped `docs(genai):`, `chore(workflow):` etc. used sparingly to
  separate audit-trail-only commits from substantive code commits.

**Negative**

- Two-commit pattern for live cards (fix with `(pending)` then a
  separate `docs(genai): backfill ...`) is slightly noisier than a
  single commit would be, but unavoidable: a card cannot reference its
  own commit hash before the commit exists. Documented as a workflow
  convention in `CLAUDE.md`.

## Alternatives considered

- **Free-form messages** — too easy to drift into "wip", "fix",
  "stuff". The panel reading `git log` would form a worse impression
  than any single bad commit could.
- **Squash on commit** — would erase the TDD rhythm. The whole point
  of preserving small commits is the storyline.
- **English without Conventional Commits prefixes** — works but loses
  the at-a-glance type filter.

## Notes

The `CLAUDE.md` workflow rules forbid autonomous `git commit`: every
commit needs explicit developer approval (`commit` / `vai` / `vamos`).
This is part of the audit-trail discipline — every commit is a human
gate, even when the AI pair did the typing.
