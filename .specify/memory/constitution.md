<!--
Sync Impact Report
- Version change: 1.1.0 → 1.2.0 (MINOR: added provenance principle, expanded governance, operating modes)
- M### AI‑Driven Solo Development
- Team Model: Expect a single human maintainer with periodic asynchronous AI (Copilot)
	contributions via small PRs.
- AI PR Guardrails: AI-authored PRs MUST include a brief rationale, the minimal change
	set, and pass CI (build, tests, lint). Human approval is required for merge.
- Slash Commands: Use chat for `/constitution`, `/specify`, `/plan`, `/tasks`, and
	`/implement`. Do not run these via terminal. Keep interactions auditable.
- Aspire‑Centric DX: Prefer .NET Aspire AppHost for local orchestration (F5 flow).
	Use docker-compose only for resources outside Aspire's purview when necessary.
- Self‑Review Discipline: For the solo maintainer, use a two-pass review (design → diff)
	and a short checklist covering principles (no band-aids), tests, observability,
	and docs.

### Operating Modes & Context Management
Three primary modes of operation guide AI-driven development sessions:

- **Provenance Mode**: Capture and extend factual history through interleaved timelines.
	Focus on anchoring each turn to exact line numbers and commit references. Exit
	criteria: all new turns anchored with verification appendix updated.
- **Alignment Mode**: Synchronize documentation and contracts to code reality. Update
	OpenAPI specs, plan/task reality check sections. Exit criteria: tests green with
	no unresolved drift items.
- **Implementation Mode**: Close defined gaps or features with minimal patches. State
	purpose, expected outcome, and validation steps before editing. Exit criteria:
	build/tests pass and contracts remain stable.

Mode Selection: If you cannot cite a specific failing test or missing capability,
stay in Provenance or Alignment—do not proceed to Implementation.

## Anti-Patterns & Risk Mitigation

Avoid these patterns that have proven problematic in practice:

- **Broad Context Grabbing**: Avoid unfocused multi-file reads that risk triggering
	autosummarization without a clear target. Process one conversation/day at a time.
- **Format Fragmentation**: Do not create parallel summary formats or duplicate
	provenance content. Maintain one canonical representation.
- **Premature Abstraction**: Do not generalize behavioral patterns or update governance
	documents before establishing a complete factual basis through provenance work.
- **Silent Parameter Removal**: Mark unused parameters as deprecated rather than deleting
	them silently. Preserve roadmap context and historical intent.
- **Blind Patching**: Never apply changes without stating purpose, expected outcome,
	and post-validation steps. Include specific failing tests or missing capabilities.
- **Multi-File Speculation**: Avoid large patches spanning many files without
	pre-declared scope and validation plan.

Risk Mitigation: When encountering ambiguity, state the specific uncertainty and
most likely resolution. Proceed with clearly-labeled assumptions for low-risk
scenarios; otherwise request explicit confirmation.ions:
  - Core Principles → added Principle VI: Evidence-Based Documentation & Provenance
  - Development Workflow & Quality Gates → added Operating Modes framework and Context Management
  - Governance → enhanced with evidence-based amendment process and compliance mechanisms
- Added sections:
  - Anti-Patterns & Risk Mitigation
  - Documentation Standards & Provenance Requirements
- Removed sections: None
- Templates requiring updates:
	- ⚠ .specify/templates/plan-template.md — should reference operating modes in constitution check
	- ⚠ .specify/templates/tasks-template.md — should align with provenance requirements
	- ✅ .specify/templates/spec-template.md — alignment maintained; focuses on feature specs
- Follow-up TODOs:
	- TODO(ADRS): Create docs/adr/ and record initial ADRs for stack choices (Aspire, Cosmos DB NoSQL + DiskANN, Ollama model)
	- TODO(LICENSE): Confirm and add LICENSE file (repository is open source and public by policy)
	- TODO(SLOS): Add docs/slo.md with concrete latency and cost targets
	- TODO(DATA-DICT): Add docs/data-dictionary.md for Steam metadata fields and normalization rules
	- TODO(TEMPLATES): Update plan and tasks templates to reference constitutional operating modes
-->

# Actual Game Search (actualgamesearch.com) Constitution

## Core Principles

### I. Open Source & Public by Default
All work in this repository is public. Discussions, issues, PRs, and design docs are
transparent by default. Private artifacts are avoided; if sensitive data or secrets
are required, they MUST be stored outside the repo and accessed via secure mechanisms
in development and production.

Rationale: Public-by-default maximizes learning, community trust, and reuse.

### II. Solve It Right, Once (No Band‑Aids)
We strongly favor solving problems correctly the first time we encounter them. Avoid
temporary workarounds and hacks that accrue debt without a tracked plan to remove
them. If a stopgap is absolutely necessary, it MUST include a test exposing the risk,
an issue link, a clear timebox, and a removal plan.

Rationale: Durable solutions reduce rework, improve reliability, and accelerate delivery.

### III. Pragmatic & Idiomatic Technology Usage
Use each technology in the way its community recommends: .NET 10 + .NET Aspire for
service orchestration and local F5, Azure Cosmos DB for NoSQL with DiskANN vectors
for hybrid semantic/full‑text search, and Ollama with an efficient embedding model
for local development. Favor small, composable designs with clear contracts.

Rationale: Idiomatic usage increases maintainability, performance, and onboarding speed.

### IV. Ultra‑Low‑Cost, High‑Accuracy Search
We are building a nontrivial, production‑ready game search that is extremely cost‑
efficient and astonishingly accurate. Design for minimal resource usage (CPU‑first
dev, efficient embeddings, index choices like DiskANN) while maintaining strong
relevance quality. Architect so this solution generalizes to broader product search
once proven.

Rationale: Cost efficiency enables sustainability; quality builds trust and utility.

### V. Test‑First, Observability, and Simplicity
Tests enforce behavior and protect against regressions. Favor simple solutions and
avoid unnecessary complexity. Implement structured logs, health checks, and minimal
tracing to explain system behavior. Prefer configuration over code for operational
concerns.

Rationale: Testability and simplicity make systems robust and evolvable.

### VI. Evidence-Based Documentation & Provenance
All significant decisions, changes, and behavioral patterns MUST be traceable to
specific evidence: line-anchored conversation excerpts, commit references, or
test results. Maintain interleaved, fact-checked timelines for complex development
sessions. Never modify foundational governance without verifiable provenance of
what necessitated the change.

Rationale: Provenance enables reliable rehydration after context loss, prevents
circular reasoning, and ensures governance evolves based on evidence rather than
speculation.

## Additional Standards & Constraints

- Security & Secrets: No secrets in the repository. Use environment variables and
	secret stores; never commit credentials. Public data only.
- Performance: Local dev baseline aims for P95 ≤ 300ms for top‑10 results on a seed
	dataset under warm conditions. Capture and version perf baselines.
- Cost Discipline: Prefer CPU‑friendly models (e.g., Embedding Gemma) for development;
	leverage DiskANN in Cosmos DB for vectors to minimize RU cost at scale.
- Accessibility: UI components must meet accessibility basics (labels, contrast,
	keyboard navigation) in the initial implementation.
- Generalizability: Design APIs and data contracts so game search can be extended to
	other product domains without major rewrites.
- Provenance & References: The 2023 “Steam Seeker” effort informs data collection and
	ranking strategies; architecture advances are captured here.

### Data Quality & ETL
- Stable Shapes, Real-World Messiness: Steam app metadata and reviews have generally
	steady shapes but contain noisy, developer-supplied fields (e.g., release dates).
	Treat inconsistencies as opportunities to demonstrate low-cost normalization with
	.NET and local Ollama models.
- Normalization & Standardization: Define deterministic normalization rules for dates,
	locales, tags, and platform fields. Capture rules in a data dictionary and unit
	tests; log counts of normalized vs raw values.
- Deduplication & Integrity: Implement duplicate and near-duplicate review detection
	(hashing + similarity thresholds). Track provenance and write idempotent ETL jobs.
- Trust & Abuse Signals: Add simple, auditable heuristics for phony/spam reviews over
	time (e.g., repetition, burstiness). Keep rules explainable and adjustable.

## Documentation Standards & Provenance Requirements

- Interleaved Timelines: For complex development sessions, maintain fact-checked,
	interleaved timelines with precise line anchors to raw conversation logs. Each
	significant turn should reference exact line numbers for verification.
- Canonical Format: Use one unified timeline format rather than multiple parallel
	summary styles. Avoid format fragmentation that obscures provenance.
- Raw-First Principle: When rehydrating context after autosummarization, read raw
	transcript segments directly rather than relying solely on derived summaries.
- Verification Commands: Include reproducible shell commands (wc, grep, git log)
	in documentation to enable independent verification of claims.
- Context Window Management: Process one conversation or day at a time to prevent
	autosummarization-induced context loss. Batch work in manageable increments.

## Development Workflow & Quality Gates

- Planning to Implementation: Use Spec → Plan → Tasks to decompose features.
- Tests: Write unit tests for business logic and minimal integration tests for APIs.
	Red‑Green‑Refactor is encouraged; tests must run in CI.
- Reviews: All changes require PR review. Reviewers verify adherence to this
	constitution and ensure no band‑aid solutions are introduced without a tracked plan.
- CI Quality Gates: Build, format/lint, unit tests, minimal perf smoke for search
	endpoints, and basic accessibility checks must pass.
- Documentation: Significant decisions recorded as ADRs. Public README and docs kept
	current with feature scope and run instructions.

### AI‑Driven Solo Development
- Team Model: Expect a single human maintainer with periodic asynchronous AI (Copilot)
	contributions via small PRs.
- AI PR Guardrails: AI-authored PRs MUST include a brief rationale, the minimal change
	set, and pass CI (build, tests, lint). Human approval is required for merge.
- Slash Commands: Use chat for `/constitution`, `/specify`, `/plan`, `/tasks`, and
	`/implement`. Do not run these via terminal. Keep interactions auditable.
- Aspire‑Centric DX: Prefer .NET Aspire AppHost for local orchestration (F5 flow).
	Use docker-compose only for resources outside Aspire’s purview when necessary.
- Self‑Review Discipline: For the solo maintainer, use a two-pass review (design → diff)
	and a short checklist covering principles (no band-aids), tests, observability,
	and docs.

## Governance

This constitution guides design and implementation across the repository. Amendments
require a PR with specific evidence: line-anchored excerpts from conversation
histories, commit references, or test failures that demonstrate the need for change.
Speculative improvements without demonstrated necessity are rejected.

**Amendment Process**:
1. Identify specific evidence (conversation line anchors, commit hashes, failing tests)
2. Draft changes with explicit rationale tied to evidence
3. Update templates and dependent artifacts for consistency
4. Increment version according to semantic versioning
5. Generate Sync Impact Report documenting all changes

**Version Semantics**:
- MAJOR: Backward‑incompatible governance changes or removals of core principles.
- MINOR: New principle/section or materially expanded guidance.
- PATCH: Clarifications or non‑semantic refinements.

**Compliance Mechanisms**:
- PR authors and reviewers ensure conformance to all principles
- Evidence-based objections to constitutional violations are binding
- The governance group (repo maintainers) may block merges that violate principles
- Regular reality checks ensure documented practices match actual behavior
- SLOs and cost targets tracked in docs and CI where feasible

**Enforcement Priority**: Evidence-based governance takes precedence over convenience.
No shortcuts around provenance requirements or operating mode discipline.

**Version**: 1.2.0 | **Ratified**: 2025-09-21 | **Last Amended**: 2025-09-24