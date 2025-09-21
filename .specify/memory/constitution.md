<!--
Sync Impact Report
- Version change: 1.0.0 → 1.1.0 (MINOR: expanded workflow guidance and data-quality standards)
- Modified sections:
  - Additional Standards & Constraints → added Data Quality & ETL subsection
  - Development Workflow & Quality Gates → clarified AI-driven solo development, review, and Aspire-centric DX
- Added sections: None (expanded within existing sections)
- Removed sections: None
- Templates requiring updates:
	- ✅ .specify/templates/spec-template.md — alignment verified; no changes required
	- ✅ .specify/templates/plan-template.md — alignment verified; no changes required
	- ✅ .specify/templates/tasks-template.md — alignment verified; no changes required
- Follow-up TODOs:
	- TODO(ADRS): Create docs/adr/ and record initial ADRs for stack choices (Aspire, Cosmos DB NoSQL + DiskANN, Ollama model)
	- TODO(LICENSE): Confirm and add LICENSE file (repository is open source and public by policy)
	- TODO(SLOS): Add docs/slo.md with concrete latency and cost targets
	- TODO(DATA-DICT): Add docs/data-dictionary.md for Steam metadata fields and normalization rules
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
require a PR describing the changes, rationale, and any migrations or enforcement
updates. Version changes follow semantic versioning:

- MAJOR: Backward‑incompatible governance changes or removals of core principles.
- MINOR: New principle/section or materially expanded guidance.
- PATCH: Clarifications or non‑semantic refinements.

Compliance: PR authors and reviewers ensure conformance. The governance group (repo
maintainers) may block merges that violate the constitution. SLOs and cost targets are
tracked in docs and CI where feasible.

**Version**: 1.1.0 | **Ratified**: 2025-09-21 | **Last Amended**: 2025-09-21