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

### Two scenarios

**Scenario A — Claude generated buggy code, user asks fix:**

> User: "Refaz GetByIdAsync — esqueceu filtro por userId. IDOR. Audit isso."
> Action: (1) Apply fix to code. (2) Append Issue Card describing
>         the original output and the fix.

**Scenario B — User fixed manually, informs Claude to log:**

> User: "Adicionei ValidateLifetime=true no JWT porque você gerou sem.
>        Registra no genai."
> Action: (1) Read current state of file, identify what changed.
>         (2) Append Issue Card describing the original mistake and
>             the manual correction.

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
[Scenario A — fixed by Claude on request | Scenario B — user manual fix logged after the fact]
```

### Constraints

- ALWAYS append to end of file. Never rewrite or reorder existing entries.
- Keep snippets short (≤10 lines each). If the function is longer,
  show only the relevant section with `// ...` markers.
- If you don't recall what was originally generated (Scenario B with
  unclear history), ASK the user to paste the original snippet.
  Do NOT fabricate.
- Issue title in English (matches Conventional Commits convention).

## Other conventions

- **Conventional Commits in English imperative** (`feat:`, `fix:`, `test:`, `refactor:`, `docs:`, `chore:`, `perf:`, `build:`, `ci:`)
- **Test-first** on Application layer use cases (red → green → refactor visible in commits)
- All async methods must accept and propagate `CancellationToken`
- All SQL must be **parameterized** (no string concatenation with input)
- Domain layer must NOT reference `Microsoft.Data.SqlClient` or `Microsoft.AspNetCore.*`
- Datetimes are UTC at boundaries (`DateTime.UtcNow`, `SYSUTCDATETIME()` in SQL)
- Authorization at resource level: every repo method that touches a user-owned resource MUST take `userId` as a required parameter (no `GetById(id)` — only `GetById(id, userId)`)
- Status codes: prefer 404 over 403 when the user's lack of access would leak existence (info leak guard)
