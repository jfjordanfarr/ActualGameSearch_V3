# Copilot Instructions for ActualGameSearch V3

### GitHub Copilot Agent Mode (outer shell)
- Guardrails: Every tool call that writes/uses network/terminal is surfaced for approval. Operate transparently.
- Cost: Charged per prompt, not per token. Favor high-agency, high-quality turns over chatter.
- Context Window Reality: Autosummarization happens periodically and is lossy. Be resilient: reopen the right files and rehydrate quickly when needed.

## Project Posture (evergreen)
- Mission: Ultra-low-cost, high-quality hybrid search for games; a model open-source project at actualgamesearch.com.
- Stack: .NET (Aspire), Cosmos DB NoSQL + DiskANN, Ollama embeddings, deterministic ranker + client-side re-ranking.
- Migration stance: We target .NET 8 today for stability. Intend to move to .NET 10 (next LTS) as soon as it’s more convenient or beneficial. Keep code/docs forward-compatible where reasonable.

## Behavioral Expectations
- Drive with agency: propose and implement, not just suggest.
- Show your work: document key findings/decisions and leave a short note in the workspace when it adds value.
- Surface risks and ambiguities early; make one small assumption max when safe and proceed; otherwise ask.
- Favor durable fixes; avoid band-aids without a removal plan.
- Use notebooks under `AI-Agent-Workspace/Notebooks/` for data exploration and short analyses.
- Stay oriented: run `AI-Agent-Workspace/Scripts/tree_gitignore.py` when disoriented; prefer concrete file paths over guesses.

## Workspace Orientation (minimap)
- API: `src/ActualGameSearch.Api/Program.cs`
- Core: `src/ActualGameSearch.Core/` (models, primitives, ranking, embeddings abstractions)
- Worker: `src/ActualGameSearch.Worker/` (ETL/ingestion)
- AppHost: `src/ActualGameSearch.AppHost/` (Aspire orchestration)
- Service defaults: `src/ActualGameSearch.ServiceDefaults/`
- Specs: `specs/002-we-intend-to/` and `specs/003-path-to-persistence/`
- Tests: `tests/*`
- Provenance corpus: `AI-Agent-Workspace/Background/ConversationHistory/**/Summarized/SUMMARIZED_*.md` (link out to Raw from these)

## Minimal Provenance & Verification
- Prefer raw-before-derived for rehydration: open the latest SUMMARIZED file, then jump to the linked Raw file to anchor.
- When you claim behavior, include a tiny verification step (build/test/grep) and record PASS/FAIL succinctly.
- Keep examples and commands short; don’t flood the context.

## Interaction Boundaries (avoid overlap with spec-kit)
- This file is about Copilot’s posture and guardrails. Keep process specifics (spec/plan/tasks prompts) in spec-kit prompts/templates.
- Treat `.specify/*` as the source of truth for spec-kit flows. Don’t restate them here.

## Example actions (trimmed)
- Create/update a focused doc or notebook under `AI-Agent-Workspace/Docs/` when you discover something non-trivial.
- Add a minimal test before changing public behavior; keep tests green.
- When lost on files/APIs, grep or open Program.cs/README/specs first; avoid hallucinating paths and names.
