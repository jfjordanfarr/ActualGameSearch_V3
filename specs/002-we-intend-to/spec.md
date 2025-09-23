# Feature Specification: Actual Game Search (Hybrid Full‑Text + Semantic)

**Feature Branch**: `002-we-intend-to`  
**Created**: 2025-09-21  
**Status**: Draft  
**Input**: User description: "We intend to create Actual Game Search (\"actualgamesearch.com\"), a fully-free nontrivial proof-of-concept for ultra-low-cost ultra-high-accuracy hybrid fulltext/semantic search over released Steam games and up to 200 reviews per game, built from independently-improvable components: a static frontend (vanilla HTML/CSS/JS, gentle preference for bootstrap; prompt input and tunable re-ranking sliders; related games exploration; image search; optional dimensionality reduction with UMAP/TriMAP and interactive 3D embeddings exploration); a DB for hybrid search (Azure CosmosDB); an Ollama instance generating embeddings with Embedding Gemma (768 dim); an API mediating search with better ranking algorithms, access/governance/rate-limiting, and optional MCP; and an ETL (likely Azure Functions App) collecting Steam metadata and reviews with filtering, modeling, precomputation (positivity, helpfulness), dedup/phony detection. Aspire-centric DX; solo developer with AI-driven workflow per the constitution."

## Execution Flow (main)
```
1. Parse user description from Input
   → If empty: ERROR "No feature description provided"
2. Extract key concepts from description
   → Identify: actors, actions, data, constraints
3. For each unclear aspect:
   → Mark with [NEEDS CLARIFICATION: specific question]
4. Fill User Scenarios & Testing section
   → If no clear user flow: ERROR "Cannot determine user scenarios"
5. Generate Functional Requirements
   → Each requirement must be testable
   → Mark ambiguous requirements
6. Identify Key Entities (if data involved)
7. Run Review Checklist
   → If any [NEEDS CLARIFICATION]: WARN "Spec has uncertainties"
   → If implementation details found: ERROR "Remove tech details"
8. Return: SUCCESS (spec ready for planning)
```

---

## ⚡ Quick Guidelines
- ✅ Focus on WHAT users need and WHY
- ❌ Avoid HOW to implement (no tech stack, APIs, code structure)
- 👥 Written for business stakeholders, not developers

### Section Requirements
- **Mandatory sections**: Must be completed for every feature
- **Optional sections**: Include only when relevant to the feature
- When a section doesn't apply, remove it entirely (don't leave as "N/A")

### For AI Generation
1. **Mark all ambiguities**: Use [NEEDS CLARIFICATION: specific question]
2. **Don't guess**: Mark unspecified items
3. **Think like a tester**: Every requirement must be testable
4. **Common underspecified areas**: user types, retention, performance, error handling, security

---

## User Scenarios & Testing (mandatory)

### Primary User Story
A visitor describes their “dream game” in a natural-language prompt and receives a
ranked list of relevant games with short explanations and quick ways to explore
similar games.

### Acceptance Scenarios
1. Given a visitor enters a prompt describing desired mechanics and themes, When they submit the query, Then the system returns a candidate pool (up to a configured cap) grouped by game with signals needed for local re-ranking and convergence filtering; the client presents an initial subset suitable for browsing.
2. Given a result game is shown, When the visitor requests similar games, Then the system shows a related set based on semantic neighbors with brief explanations.
3. Given a visitor adjusts relevance sliders (e.g., semantic vs textual weight, positivity, richness, playtime), When they re-rank locally, Then the ordering of results updates without a new server search.
4. Given a visitor toggles a convergence filter (e.g., require ≥2 matching reviews per game, or require ≥1 matching review and a game-description match), When they apply it, Then only games satisfying the convergence condition remain.
5. Given convergence filtering is off, When the visitor views results, Then they can optionally include “singletons” (games with exactly one matching review) as discoverable “dark matter.”

### Edge Cases
- Empty or extremely short queries are politely rejected with guidance to refine the prompt.
- Very long queries are truncated or summarized before processing while preserving intent.
- Queries with no strong matches return a fallback set with a message explaining low confidence and suggestions to refine.
- If the candidate set exceeds a safe cap, the system returns the top subset with a note that more exist and may require additional refinement.

## Requirements (mandatory)

### Functional Requirements
- FR-001: The system MUST accept a free-text search prompt and return a candidate set suitable for client-side re-ranking.
- FR-002: The client MUST present an initial subset of candidates with title, image (if available), short description, and 1–3 short evidence snippets that justify the match; the subset size MUST be configurable.
- FR-003: The system MUST support a “similar games” action from any result, returning a related set with brief explanations.
- FR-004: The client MUST support dynamic local re-ranking via user-adjustable weights (e.g., semantic vs textual match, positivity, richness, playtime), without requiring a new server query.
- FR-005: The system MUST provide actionable, non-technical error messages (no stack traces).
- FR-006: The system MUST handle up to 200 reviews per game as part of relevance evidence and similarity calculations.
- FR-007: The system MUST enforce basic rate limiting to protect from abuse.
- FR-008: The system MUST log query metadata and errors without PII for monitoring quality and performance.
- FR-009: The system SHOULD allow exploration of related games via multiple pivots (e.g., tags, mechanics, themes).
- FR-010: The system MAY support visual exploration of embeddings (2D/3D) as an optional enhancement.
- FR-011: The experience MUST be responsive and accessible.
- FR-012: The system MUST provide “similar games” from a game detail view as well as from the search results.
- FR-013: The system MUST provide deterministic, auditable handling for duplicate or near-duplicate reviews, and noisy metadata fields.

### Filtering & Constraints
- FR-020: The system MUST support filtering by release date (e.g., on/after 2024‑01‑01), controller support (full or partial), adult content flag (exclude 18+), minimum number of reviews (e.g., > 20), purchase-origin ratio (e.g., > 80% purchases among indexed reviews), and content type (game vs DLC).
- FR-021: The system MUST apply filters server-side before returning candidates.
- FR-022: The system MUST group candidate results by game identifier to detect and surface cases where multiple reviews from the same game match (“pileups”).
- FR-023: The system MUST include indicators for both game-level and review-level matches so the client can reward items matching on both, and compute a simple convergence signal.
- FR-024: The system MUST support returning a larger candidate set (e.g., up to 2,000 grouped by game) to enable client-side re-ranking.
- FR-025: The system MUST provide a clear limit and message when candidate caps are reached, and MUST allow the user to adjust the candidate cap within safe bounds (e.g., 200–2,000) to balance breadth vs noise. The system MAY support paging/streaming for candidates beyond the cap; when not supported, it MUST indicate that additional results exist and suggest refining the query.
- FR-026: The purchase-origin ratio MUST be computed over a bounded, indexed review sample per game with a hard cap of up to 10,000 reviews, ordered by helpfulness or recency when available. The system MUST respect upstream API fair‑use via throttling/backoff and documented query limits.
- FR-027: The system MUST allow a convergence filter: (A) minimum matching reviews per game (configurable, e.g., ≥2), and/or (B) at least one matching review plus a game-description match.
- FR-028: The system MUST allow including/excluding “singleton” games (exactly one matched review) to surface additional discovery (“dark matter”).

### Payload for Client Re‑Ranking (WHAT the client needs)
- FR-030: Each candidate item MUST include fields enabling client re-ranking:
  - Textual match score and semantic match score (per game).
  - Count of matching reviews per game and aggregate review-match score.
  - Convergence indicators: reviewsMatchedCount, gameTextMatched (boolean), convergenceSatisfied (boolean), convergenceType (e.g., "review_pileup", "review+game").
  - Positivity signals (e.g., share of positive reviews) and helpfulness signals.
  - Richness metrics (e.g., token/word count and uniqueness proxy for game description and/or matched reviews).
  - Playtime metrics (e.g., average playtime across matched reviews).
  - Filter-relevant flags/values (release date normalized, controller support, adult content flag, review count, purchase-origin ratio, content type).
- FR-031: The system MUST include lightweight evidence snippets (1–3) per candidate suitable for brief explanations.
- FR-032: Default client re-rank weights MUST treat semantic and textual signals equally (50/50). If the provider/API exposes independently normalized semantic and textual scores, the client MAY expose independent sliders anchored to normalized ranges and persist user preferences.

### Key Entities (payload shapes, not implementation)
- Candidate: gameId, title, image, shortDescription, evidenceSnippets[1..3], scores {textual, semantic, reviewAggregate}, counts {reviewsMatched}, convergence {reviewsMatchedCount, gameTextMatched, satisfied, type}, signals {positivity, helpfulness}, metrics {richness, avgPlaytime}, filters {releaseDate, controllerSupport, adultFlag, reviewCount, purchaseOriginRatio, contentType}.
- FilterSet: releaseOnOrAfter, controllerSupport, excludeAdult, minReviews, minPurchaseOriginRatio, contentType, convergenceMode, minMatchingReviews, includeSingletons, candidateCap.
- ReRankWeights: semanticVsTextual (default 0.5/0.5), positivity, richness, playtime, popularity/novelty.

---

## Review & Acceptance Checklist

### Content Quality
- [ ] No implementation details
- [ ] Focused on user value and business needs
- [ ] Written for non-technical stakeholders
- [ ] All mandatory sections completed

### Requirement Completeness
- [x] No [NEEDS CLARIFICATION] markers remain
- [ ] Requirements are testable and unambiguous
- [ ] Success criteria are measurable
- [ ] Scope is clearly bounded
- [ ] Dependencies and assumptions identified

---

## Execution Status

- [x] User description parsed
- [x] Key concepts extracted
- [x] Ambiguities marked
- [x] User scenarios defined
- [x] Requirements generated
- [x] Entities identified
- [ ] Review checklist passed

---

## Reality Check (as of 2025-09-23)
- Endpoints implemented: `/api/search/games`, `/api/search/reviews`, `/api/search`.
- Implemented query params: `q` (all), `top` (cap), `fields=full` (reviews/grouped only). Planned params like `candidateCap`, `controller`, `adultOnly`, and explicit convergence filters are not yet wired in the API.
- Result envelope: `{ ok, data, error }` matches contract tests and current implementation.
- Aspire AppHost provisions Cosmos emulator and Ollama container; API falls back to in-memory repos in test mode.
