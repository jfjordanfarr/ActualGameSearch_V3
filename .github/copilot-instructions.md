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
- **Check if we have already solved a problem before**: all development history is inside the `./AI-Agent-Workspace/Background/ConversationHistory/` folder. Use `rg` to search for keywords and find relevant `SUMMARIZED_*.md` files. Open the latest summary first, then follow links to the raw conversation for context.

Here's a quick shell one-liner to find all the summaries:
```bash
find /workspaces/ActualGameSearch_V3/AI-Agent-Workspace/Background/ConversationHistory -path "*/Summarized/SUMMARIZED_*.md" -type f | sort -t/ -k6 -V | awk -F/ '{spec=$(NF-2); file=$(NF); gsub(/SUMMARIZED_|\.md/, "", file); printf "%-25s %s\n", spec":", file}'
```

## Workspace Orientation (minimap)
- API: `src/ActualGameSearch.Api/Program.cs`
- Core: `src/ActualGameSearch.Core/` (models, primitives, ranking, embeddings abstractions)
- Worker: `src/ActualGameSearch.Worker/` (ETL/ingestion)
- AppHost: `src/ActualGameSearch.AppHost/` (Aspire orchestration)
- Service defaults: `src/ActualGameSearch.ServiceDefaults/`
- Specs: `specs/002-we-intend-to/` and `specs/003-path-to-persistence/`
- Tests: `tests/*`
- Provenance corpus: `AI-Agent-Workspace/Background/ConversationHistory/**/Summarized/SUMMARIZED_*.md` (link out to Raw from these)
- **Ollama 8K Context**: [DEFINITIVE SOLUTION](../AI-Agent-Workspace/Docs/ollama-8k-context-solution.md) - Never lose this again!

## Minimal Provenance & Verification
- Prefer raw-before-derived for rehydration: open the latest SUMMARIZED file, then jump to the linked Raw file to anchor.
- When you claim behavior, include a tiny verification step (build/test/grep) and record PASS/FAIL succinctly.
- Keep examples and commands short; don’t flood the context.

## Interaction Boundaries (avoid overlap with spec-kit)
- This file is about Copilot’s posture and guardrails. Keep process specifics (spec/plan/tasks prompts) in spec-kit prompts/templates.
- Treat `.specify/*` as the source of truth for spec-kit flows. Don’t restate them here.

## C# Coding Conventions

- Optimize for human readability first and foremost (use the full expressiveness of spacing in the ways that veteran SQL and Powershell developers do).
- Try to avoid magic strings; compartmentalize them in appropriate configurations/constants while accepting some practical exceptions. 
- Be a showcase of the very best practices of modern C#, .NET 8+, and .NET Aspire
- (**Unique guideline for agentic development**) When a file gets longer than 500 lines, refactor. Edit tools become unreliable at longer file lengths.