# Copilot Instructions for ActualGameSearch V3

### Github Copilot Agent Mode
- **VS Code Automatic Guardrails for Agent Mode**: Every LLM tool call which writes to the disk, utilizes the network, or leverages the terminal, is surfaced to the user in the UI for approval before execution. Github Copilot Agent Mode has been deployed across enterprises worldwide due to its exceptionally robust safety and compliance features, ensuring that all actions taken by the AI are transparent and under user control.
- **Cost Structure**: The user is charged **per prompt**, **not per token**. This is to encourage high quality, high-agency interactions.
- **Context Window:** To facilitate development sessions of any arbitrary length, an auto-summarization mechanism is employed. When the current context window exceeds 96k tokens, the chat is automatically summarized, and a new context window is started based on that summary. This operation is lossy and happens commonly. It is normal to need to refer back to the original files or documentation to recover lost context. 
- **Conversation History**: To avoid common circular reasoning pitfalls, the entire conversation history is available in both raw and summarized forms in the `AI-Agent-Workspace/Background/ConversationHistory/` directory. When things fail, search here to see if you've overcome that same failure before. 

## Project Mission
Deliver ActualGameSearch: a sustainable, open-source, ultra-low-cost, high-quality hybrid fulltext/semantic game search engine, with a focus on discoverability, user experience, and best-practices architecture. The goal is to serve as a model for hybrid search in the open-source community and to provide a genuinely valuable public search experience at actualgamesearch.com.

## Priorities
1. Deliver astonishingly relevant game search and relatedness navigation.
2. Operate at minimal cost (showcase genuine engineering value).
3. Provide a free, public search experience at actualgamesearch.com (showcase genuine consumer value).
4. Serve as a model open-source project for hybrid search over products.
5. Be a nontrivial living example of AI-**driven** development, thoroughly documented in findings and process.
6. Learn, document, and seek genuine insights from the data collected (showcase genuine data science value).

## Behavioral Expectations
- **Take high agency**: You are expected to drive. You have every single tool you need to succeed at your disposal. Every LLM tool call which writes to the disk, utilizes the network, or leverages the terminal, is surfaced to the user in the UI for approval before execution, and many are rejected. 
- Propose and implement solutions, not just code snippets.
- Document findings, tradeoffs, and next steps in the workspace.
- Surface blockers, ambiguities, or risks immediately.
- If unsure, ask for clarification, but otherwise proceed.
- Avoid accruing technical debt; show a strong preference for solving a problem correctly and completely, and a strong aversion to placeholders.
- Use Python notebooks (`AI-Agent-Workspace/Notebooks/`) for rapid data exploration, prototyping, and documentation of findings.
- **Always** stay oriented about directory structure with the `AI-Agent-Workspace/Scripts/tree_gitignore.py` script.

## Always-True Project Facts (Stable Grounding)
These are deliberately evergreen anchors distilled from the fact‑checked interleaved timelines (Days 1–4). They should be assumed true unless an intentional future governance or architectural change (PR + docs update) explicitly revises them.

### Identity & Mission Snapshot
- Project: ActualGameSearch (hybrid semantic + lightweight full‑text game search; model open-source exemplar of low-cost, high‑relevance search).
- Stack Core: .NET (Aspire for local orchestration), Cosmos DB NoSQL + DiskANN vectors, Ollama (local embedding model; default nomic‑embed‑text / embedding gemma candidate), deterministic ranker + client re‑ranking philosophy.
- Primary Interaction Style: AI‑driven solo development (one human maintainer + Copilot acting with high agency under provenance guardrails).

### Solution Layout (Stable Projects)
- `src/ActualGameSearch.Api` – Minimal API (search endpoints, returns `Result<T>` envelope, static frontend assets under `wwwroot/`).
- `src/ActualGameSearch.Core` – Domain primitives (`Result<T>`, ranking logic, models, embeddings abstractions, repositories interfaces).
- `src/ActualGameSearch.Worker` – ETL / seeding / embedding ingestion jobs (small authentic data batches; later scalable ingestion).
- `src/ActualGameSearch.AppHost` – Aspire orchestration (Cosmos emulator, Ollama resource, service wiring, environment bootstrap).
- `src/ActualGameSearch.ServiceDefaults` – Cross‑cutting defaults (observability, configuration helpers, Aspire conventions).
- `tests/*` – Unit, Integration, Contract test suites (must remain green; baseline health signal before/after changes).
- `specs/002-we-intend-to/*` – Living product specification set (`spec.md`, `plan.md`, `tasks.md`, `contracts/openapi.yaml`, `quickstart.md`).
- `AI-Agent-Workspace/Background/ConversationHistory/Summarized/FACTCHECKED_Interleaved_*.md` – Canonical provenance corpus (never summarize without line anchors).

### Stable API Contract (Current Implemented Endpoints)
All return a JSON envelope: `Result<T> { ok: bool, data: <payload|null>, error: string|null }`.
- `GET /api/search/games?q=...&top=K` – Game-first search (grouped by game).
- `GET /api/search/reviews?q=...&top=K[&fields=full]` – Review-centric search; `fields=full` may include expanded review/game text.
- `GET /api/search?q=...&top=K[&fields=full]` – Grouped/combined discovery variant.
Deprecated/Planned (documented but not wired): convergence params, `candidateCap` alias, weight tuning params, future `/api/similar/{gameId}`. These MUST remain clearly marked (OpenAPI `deprecated: true`) until implemented.

### Enduring Behavioral Guardrails
1. Provenance First: Do not modify foundational docs (`copilot-instructions.md`, spec/plan/tasks) without fact‑checked, line‑anchored evidence of drift.
2. One Canonical Format: Interleaved fact‑checked timelines are the single source for historical reasoning—never introduce parallel summary formats.
3. Raw Before Derived: Read raw transcript segments (not only prior summaries) when rehydrating after autosummarization.
4. Early Stop Criterion: Stop discovery once you can name (a) exact files to change, (b) affected public contracts, (c) intended tests to run/update.
5. Never Blind-Patch: For each patch, state purpose, expected outcome, and post‑validation steps (tests/build) before editing.
6. Prefer Additive + Deprecation Over Removal: Mark unused or not-yet-wired parameters deprecated rather than deleting—preserves roadmap context.
7. Keep Tests Green: Never leave tree in failing state; if failing after 3 focused attempts, summarize root cause + options.
8. Reality > Spec Drift: If code and spec disagree, treat runtime behavior as ground truth unless it violates a principle; then fix code + document rationale.
9. Minimal Surface Changes: Touch only the files necessary, avoid broad reformatting that obscures provenance diffs.
10. Explicit Verification Commands: When asserting state/behavior, include reproducible shell commands (e.g., `grep`, `wc -l`, `dotnet test`).

## Operating Modes & Execution Pattern
| Mode | Purpose | Primary Outputs | Exit Criteria |
|------|---------|-----------------|---------------|
| Provenance | Capture/extend factual history | Interleaved turn entries (anchors, excerpts) | All new turns anchored + verification appendix updated |
| Alignment | Sync docs/contracts to code reality | Updated OpenAPI, spec/plan/tasks Reality Check sections | Tests green + no unresolved drift items |
| Implementation | Close a defined gap or feature | Minimal patch + tests + doc delta | Build/tests pass; contract stable |

Mode Selection Heuristic: If you cannot cite a specific failing test or missing capability yet, you are not in Implementation—stay in Provenance or Alignment.

## Rehydration Playbook (Post Autosummarization / Fresh Session)
Perform these steps (often partially) at the start of any substantial turn:
1. Inspect Provenance: Open the most recent `FACTCHECKED_Interleaved_*.md` to recall last anchored turn and pending gaps.
2. Workspace Shape:
	- Run structure script: `python AI-Agent-Workspace/Scripts/tree_gitignore.py | head -n 120` (avoid full dump unless needed).
3. Validation Baseline:
	- `dotnet test -c Debug --nologo` (quick health; if slow, target specific csproj first).
4. Contract Reality Check (only if relevant to change):
	- Open `specs/002-we-intend-to/contracts/openapi.yaml` + `src/ActualGameSearch.Api/Program.cs` and confirm endpoint param alignment.
5. Drift Scan (optional quick grep examples):
	- `grep -R "candidateCap" -n src/` (should surface only deprecated references if any)
	- `grep -R "Result<" -n src/ActualGameSearch.Api`
6. Decide Mode (Provenance / Alignment / Implementation) and state it explicitly in your preamble.
7. Execute minimal batch (≤5 tool actions) → validate → iterate.

## Patch & Validation Checklist (Per Change Batch)
Before Editing:
- State: goal, scope (files), expected diffs, mode.
After Editing:
- Run: `dotnet build` (or rely on test compile), `dotnet test` (targeted if large), optional smoke: `curl http://localhost:8080/api/search/games?q=test&top=1` (when API running).
- Record: PASS/FAIL per gate and remediation or deferral note.

## Contract & Documentation Alignment Policy
When discovering drift:
1. Confirm runtime behavior (read `Program.cs`, relevant service/repository files).
2. Update OpenAPI to reflect actual accepted params; mark speculative ones deprecated (never silently remove without historical note).
3. Add or refresh “Reality Check” sections in `spec.md` & `plan.md`; add Status Snapshot in `tasks.md`.
4. If new param/route introduced: add minimal contract test before implementation if feasible.
5. Link provenance: reference Day N Turn M where the intent originated (optional comment or commit message footnote).

## Stable Endpoints – Quick Reference
```text
GET /api/search/games      q, top
GET /api/search/reviews    q, top, fields=full (optional)
GET /api/search            q, top, fields=full (optional)
Envelope: { ok, data, error }
Deprecated (in OpenAPI only): candidateCap, convergence.*, weights.*, minReviews (planned)
```

## Common File Links (Relative)
- Constitution: `/.specify/memory/constitution.md`
- Spec: `/specs/002-we-intend-to/spec.md`
- Plan: `/specs/002-we-intend-to/plan.md`
- Tasks: `/specs/002-we-intend-to/tasks.md`
- OpenAPI: `/specs/002-we-intend-to/contracts/openapi.yaml`
- Quickstart: `/specs/002-we-intend-to/quickstart.md`
- Provenance Corpus: `/AI-Agent-Workspace/Background/ConversationHistory/Summarized/`
- Tree Script: `/AI-Agent-Workspace/Scripts/tree_gitignore.py`
- API Entry: `/src/ActualGameSearch.Api/Program.cs`
- Worker Entry: `/src/ActualGameSearch.Worker/Program.cs`
- Core Models: `/src/ActualGameSearch.Core/Models/`
- Primitives: `/src/ActualGameSearch.Core/Primitives/Result.cs`

## Minimal Run Commands
Run API only (in-memory mode if Cosmos/Ollama absent):
```bash
dotnet build src/ActualGameSearch.Api/ActualGameSearch.Api.csproj -c Debug
DOTNET_RUNNING_IN_TESTHOST=true ASPNETCORE_URLS=http://localhost:8080 \
  dotnet run --project src/ActualGameSearch.Api/ActualGameSearch.Api.csproj
```
Run Aspire AppHost (brings up Cosmos emulator + Ollama resource if configured):
```bash
dotnet build src/ActualGameSearch.AppHost/ActualGameSearch.AppHost.csproj -c Debug
dotnet run --project src/ActualGameSearch.AppHost/ActualGameSearch.AppHost.csproj
```
Seed / ETL Worker (after resources up):
```bash
dotnet run --project src/ActualGameSearch.Worker/ActualGameSearch.Worker.csproj
```

## Verification Command Snippets (Copy-Paste)
```bash
# List top-level projects
find src -maxdepth 2 -name '*.csproj'

# Confirm endpoints present
grep -n "MapGet" src/ActualGameSearch.Api/Program.cs | head -n 40

# Contract vs implementation param audit (example)
grep -n "fields=full" -R src/ActualGameSearch.Api

# Test health (fast path)
dotnet test tests/ActualGameSearch.UnitTests/ActualGameSearch.UnitTests.csproj --nologo

# Full test sweep
dotnet test --nologo
```

## Always Provide (Implicit Short Context Seed) at Start of a Major Turn
When responding after a context loss or before a substantial multi-file action, internally ground on:
```
Project: ActualGameSearch – .NET Aspire + Cosmos (vectors) + Ollama embeddings – endpoints: /api/search(/games|/reviews) returning Result<T>.
Mode: (Provenance|Alignment|Implementation) = <choose one>.
Goal: <single concise objective>.
Next Validation: dotnet test (targeted) + contract param check.
Early Stop Trigger: once exact file list + tests known.
```
Do NOT echo verbatim every time to the user unless clarifying; use it to self-orient.

## Commit & Message Hygiene
- Group logically related changes (one feature / alignment per commit).
- Reference provenance source in commit body: `Provenance: Day2 Turn16` (optional but encouraged for traceability).
- Avoid mixing refactors with functional changes unless trivial and validated by tests.

## Drift Repair Playbook (Condensed)
```
1. Detect symptom (test failure, spec mismatch, runtime log).
2. Confirm actual behavior (read code + run minimal reproduction).
3. Classify: (Spec drift | Missing implementation | Outdated test).
4. If Spec drift → update docs + mark planned params deprecated.
5. If Missing implementation → write/adjust test first, then implement.
6. If Outdated test → adjust test after confirming design intent in spec.md.
7. Re-run tests; record PASS/FAIL in response.
```

## Anti-Patterns to Avoid
- Broad, unfocused file reads that risk autosummarization without a stated target.
- Adding new summary formats or duplicating provenance content.
- Silent parameter removal (instead: deprecate with reason + date).
- Multi-hundred line patches without pre-declared scope + validation plan.
- Relying solely on earlier summaries instead of raw transcript when reconstructing intent.

## Escalation Guidance
If blocked after reasonable deduction (e.g., ambiguous spec clause, missing external dependency):
1. State the specific ambiguity and the most likely resolution.
2. Proceed with a clearly-labeled assumption if low risk; otherwise request explicit confirmation.
3. Record assumption in modified doc (Reality Check or Status Snapshot) if it affects design.

---
This enriched instruction set was synthesized from the four fact‑checked interleaved timelines; modify only with accompanying provenance references and updated verification commands.

## Example Actions
- If you need to log or document learnings, create or update a notebook or markdown file in `AI-Agent-Workspace/Docs`.
- If you see a way to improve the architecture, propose and implement it.
- If you encounter a blocker, document it and suggest a workaround.
- If your LLM search tools are failing to locate a file you're confident exists, run `python ./AI-Agent-Workspace/Scripts/tree_gitignore.py` to get a tree view of the present working directory or point the script at a subdirectory to confirm the file's presence.

---