# Project Conventions for Claude Code

## Language convention (foundational — applies to all artifacts)

The invariant is **artifact language**, not operator language. Operator
interaction with AI tooling can be any natural language; what ships in the
repository is English, always.

- **Repository artifacts: English only.** This includes:
  - Source code (identifiers, classes, methods, variables, namespaces)
  - Inline comments (when present — default is no comments)
  - Commit messages (Conventional Commits in imperative English)
  - All Markdown docs (README, ADRs, architecture.md, genai.md, planning-session.md)
  - Test names (e.g., `Should_return_404_when_resource_belongs_to_other_user`)
  - Issue Card titles and bodies in docs/genai/issues.md
  - Log messages (Serilog templates)
  - Error messages, ProblemDetails titles, validation messages
  - UI strings in the Angular frontend (login form, snackbars, empty states, errors)
  - Seed data (emails like `demo@ballastlane.test`, categories like
    `Food`/`Transport`, sample descriptions)
  - Branch names, PR titles, git tags
  - JSON property names (camelCase, English)
  - `.env` and `appsettings.json` keys

- **Operator interaction with AI tooling:** any natural language is acceptable.
  The artifact-language invariant above must hold regardless of the language
  used to chat with the AI.

**Rule of thumb:** if a string will end up in a file that is committed to the
repository, it is English. No exceptions. Catch yourself before save: if the
string is non-English, translate before writing the file.

## GenAI Audit Trail (mandatory directive)

This project maintains `docs/genai/issues.md` as a live audit log of
AI-generated code that required correction. The headline document
`docs/genai.md` is the deliverable's summary and points to the audit
trail; the cards themselves are appended to `docs/genai/issues.md`.
Whenever the user invokes a trigger phrase in the same turn as a fix
request, you MUST append an Issue Card to `docs/genai/issues.md` in
addition to applying the code fix.

### Trigger phrases (any of these in the user message)

- "audit isso" / "audita isso"
- "registra no genai" / "registre no genai"
- "issue card"
- Explicit: "log this in docs/genai/issues.md"

Without a trigger phrase, do NOT log. Apply only the code fix.

### Four source paths

**1. Live trigger — Claude generated buggy code, user invokes phrase mid-work:**

> User: "Refaz GetByIdAsync — esqueceu filtro por userId. IDOR. Audit isso."
> Action: (1) Apply fix to code. (2) Append Issue Card describing
>         the original output and the fix.

**2. User manual fix — user fixed manually, informs Claude to log:**

> User: "Adicionei ValidateLifetime=true no JWT porque você gerou sem.
>        Registra no genai."
> Action: (1) Read current state of file, identify what changed.
>         (2) Append Issue Card describing the original mistake and
>             the manual correction.

**3. AI cleanup pass — at sprint boundary, on user request:**

> User: "faz o cleanup pass do sprint X"
> Action: Read `git log` + commit diffs for that sprint. Identify 5-7
>         substantive issues that were fixed during the work but never
>         logged. Append cards anchored to commit hashes.

**4. Human review (AI blind spot) — user catches what Claude's cleanup missed:**

> User: "tô lendo o code e percebi que X tá errado / faltou / é frágil"
>        (and may ask Claude to help format the card; the catch is human)
> Action: Append Issue Card whose `Human revalidation` body summarizes
>         the human's catch in the user's voice. The human voice in that
>         section is the meta-signal that a human gate is closing on
>         what Claude's self-review missed.

### Two card templates, two sections

`docs/genai/issues.md` has two top-level sections, each with its own
template. The SECTION the card lives in IS the encoding of the catch
source — there is no `### Source` label, and no other field needs to
be filled to identify the origin.

#### Template A — Cleanup-pass cards (Claude self-audit)

Goes under `## Cleanup-pass cards (Claude self-audit)` in `issues.md`.
Used when Claude reads sprint-boundary git diffs and surfaces issues
that escaped real-time logging (trigger path 3). Five sections;
`### Human revalidation` is intentionally **empty** — its emptiness
IS the signal of automated origin.

```markdown
## Issue — [short technical title in English]

### What AI generated
[Description of what was previously generated. Prose by default;
 include a code snippet only when essential. ≤10 lines if used.]

### Why it's wrong
[Technical analysis of the failure: e.g., UTC bypass, missing
 Enum.IsDefined, undue coupling. Impact-focused.]

### What was done
[Record of the final implementation applied. Prose by default; short
 code snippet only when essential. ≤10 lines if used. Close with the
 commit hash where the fix landed (or `(pending)` if not yet committed).]

### Human revalidation
[Leave the body empty. The absence of a human voice in this section
 IS the signal that this entry came from Claude's self-audit.]
```

#### Template B — Human-discovered cards (usage + direct audit)

Goes under `## Human-discovered cards (usage + direct audit)` in
`issues.md`. Used when the developer surfaces an issue through direct
usage of the running app, manual code review, or a live trigger during
a fix session (trigger paths 1, 2, 4). Three sections; the entire card
is in the developer's voice, so a separate "Human revalidation"
section is redundant. The "What was done" section may include a later
review note if the original fix was revisited (e.g., "sprint-boundary
review confirmed the fix is appropriately sized").

```markdown
## Issue — [short technical title in English]

### What I observed
[The behavior the developer encountered or the design gap they noticed.
 Concrete and specific: what the user did, what the app did, what looked
 wrong. First-person framing because the human IS the source of truth.]

### Why it's wrong
[Technical analysis of the failure. Same shape as Template A.]

### What was done
[Record of the fix applied. Same shape as Template A. May include a
 trailing review note if the fix was revisited at a sprint boundary
 (e.g., "Sprint-boundary review: existing fix sized appropriately;
 considered hardening X but rejected as over-engineering.").]
```

### Constraints

- ALWAYS append to the END of the appropriate section in
  `docs/genai/issues.md`. Trigger path 3 (cleanup pass) → Cleanup-pass
  section. Trigger paths 1, 2, 4 (live, manual, human review) →
  Human-discovered section. Never rewrite or reorder existing entries.
  Never edit `docs/genai.md` for a card — the headline file holds only
  the summary and the count.
- Keep snippets short (≤10 lines each). If the function is longer,
  show only the relevant section with `// ...` markers.
- If you don't recall what was originally generated (User manual fix with
  unclear history), ASK the user to paste the original snippet.
  Do NOT fabricate.
- Issue title in English (matches Conventional Commits convention).

## Workflow

- **Never run `git commit` autonomously.** After making changes that would
  normally end in a commit, stage them (or leave unstaged), summarize what
  changed in 1-3 lines, and STOP. Wait for the user to say `commit` / `vai`
  / `vamos` (or equivalent explicit approval) before running `git commit`.
  Auto-mode does NOT imply auto-commit. Commits are part of the deliverable
  for this take-home — every one needs a human gate.
- Same rule for `git push`, `git tag`, `git rebase`, `git reset`, and any
  other operation that mutates published or hard-to-reverse state.
- **Session handoff is conversational, not a committed file.** Do not
  introduce `_HANDOFF.md` / `HANDOFF.md` / `SESSION.md` (or equivalent
  operational-status documents) into the repository. Cross-session
  continuity lives in the chat transcript at session boundaries and is
  pasted into fresh sessions as the bootstrap message. Repository
  artifacts are deliverables; operational metadata is not.

## Other conventions

- **Conventional Commits in English imperative** (`feat:`, `fix:`, `test:`, `refactor:`, `docs:`, `chore:`, `perf:`, `build:`, `ci:`)
- **Test-first** on Application layer use cases (red → green → refactor visible in commits)
- All async methods must accept and propagate `CancellationToken`
- All SQL must be **parameterized** (no string concatenation with input)
- Domain layer must NOT reference `Microsoft.Data.SqlClient` or `Microsoft.AspNetCore.*`
- Datetimes are UTC at boundaries (`DateTime.UtcNow`, `SYSUTCDATETIME()` in SQL)
- Authorization at resource level: every repo method that touches a user-owned resource MUST take `userId` as a required parameter (no `GetById(id)` — only `GetById(id, userId)`)
- Status codes: prefer 404 over 403 when the user's lack of access would leak existence (info leak guard)
