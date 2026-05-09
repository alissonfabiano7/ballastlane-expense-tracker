# Project Conventions for Claude Code

## Language convention (foundational — applies to all artifacts)

- **Conversation with user:** Portuguese (BR). Reply in PT-BR for explanations,
  questions, status updates, and end-of-turn summaries.
- **Repository artifacts:** English only. This includes:
  - Source code (identifiers, classes, methods, variables, namespaces)
  - Inline comments (when present — default is no comments)
  - Commit messages (Conventional Commits in imperative English)
  - All Markdown docs (README, ADRs, architecture.md, genai.md, planning-session.md)
  - Test names (e.g., `Should_return_404_when_resource_belongs_to_other_user`)
  - Issue Card titles and bodies in docs/genai.md
  - Log messages (Serilog templates)
  - Error messages, ProblemDetails titles, validation messages
  - UI strings in the Angular frontend (login form, snackbars, empty states, errors)
  - Seed data (emails like `demo@ballastlane.test`, categories like `Food`/`Transport`,
    sample descriptions). Never `Alimentação`, always `Food`.
  - Branch names, PR titles, git tags
  - JSON property names (camelCase, English)
  - `.env` and `appsettings.json` keys

**Rule of thumb:** if a string will end up in a file committed to the repo, it's
English. No exceptions. If you catch yourself writing a Portuguese string into
a file, stop and translate before saving.

## GenAI Audit Trail (mandatory directive)

This project maintains `docs/genai.md` as a live audit log of AI-generated
code that required correction. Whenever the user invokes a trigger phrase
in the same turn as a fix request, you MUST append an Issue Card to
`docs/genai.md` in addition to applying the code fix.

### Trigger phrases (any of these in the user message)

- "audit isso" / "audita isso"
- "registra no genai" / "registre no genai"
- "issue card"
- Explicit: "log this in docs/genai.md"

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
> Action: Append Issue Card with Source line ending in `(AI blind spot)` so
>         the panel reading the doc sees the meta-signal: human gate is
>         catching things Claude's self-review missed.

### Strict Issue Card template

```markdown
## Issue — [short technical title in English]

### What AI generated
\`\`\`csharp
[≤10 lines snippet of buggy code, verbatim]
\`\`\`

### Why it's wrong
[1-2 sentences. Impact-focused: IDOR, info leak, resource leak,
 timezone bug, DoS vector, etc. No fluff.]

### What was committed
\`\`\`csharp
[≤10 lines snippet of corrected code]
\`\`\`

### Source
[one of the four labels below, plus a short context phrase + commit hash]
```

### Source taxonomy (mandatory — pick exactly one per card)

- **Live trigger** — user invoked the trigger phrase mid-work; Claude applied
  the fix and wrote the card in the same turn.
- **User manual fix** — user corrected the code themselves, then asked Claude
  to log retroactively (often pasting the original snippet for accuracy).
- **AI cleanup pass** — Claude self-noticed the issue at the end of a sprint
  while reading `git log` + diffs; backfilled with reference to the commit
  hash where the fix landed.
- **Human review (AI blind spot)** — user, reading the codebase as a human
  reviewer after Claude's cleanup pass had completed, surfaced an issue
  Claude failed to flag. Strongest authenticity signal — proves there is a
  human gate beyond AI's self-audit. Always tagged with `(AI blind spot)`
  so a panel reader notices the meta-signal.

Format the Source line as a single line, e.g.:
```
Human review (AI blind spot) — surfaced during user's manual review of the
Application layer; commit `abc1234`.
```

### Constraints

- ALWAYS append to end of file. Never rewrite or reorder existing entries.
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

## Other conventions

- **Conventional Commits in English imperative** (`feat:`, `fix:`, `test:`, `refactor:`, `docs:`, `chore:`, `perf:`, `build:`, `ci:`)
- **Test-first** on Application layer use cases (red → green → refactor visible in commits)
- All async methods must accept and propagate `CancellationToken`
- All SQL must be **parameterized** (no string concatenation with input)
- Domain layer must NOT reference `Microsoft.Data.SqlClient` or `Microsoft.AspNetCore.*`
- Datetimes are UTC at boundaries (`DateTime.UtcNow`, `SYSUTCDATETIME()` in SQL)
- Authorization at resource level: every repo method that touches a user-owned resource MUST take `userId` as a required parameter (no `GetById(id)` — only `GetById(id, userId)`)
- Status codes: prefer 404 over 403 when the user's lack of access would leak existence (info leak guard)
