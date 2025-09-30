# Data Model: 003 – Path to Persistence

Date: 2025-09-25

## Entities

### Run
- id: string (ulid)
- stage: enum [ingest, refine, derive]
- startedAt/finishedAt: datetime (UTC)
- params: json (cadences, caps, sampling mode)
- outcome: enum [success, partial, failed]
- metrics: { itemsProcessed, externalCalls, avoidedCalls, errors, durations }

### RawArtifact
- runId: string
- appId: int (nullable for catalog-wide artifacts)
- type: enum [reviews, storePage, news, catalog]
- path: string (relative to data lake)
- timestamp: datetime
- checksum: string (sha256)
- bytes: int

### Review (Bronze)
- appId: int
- reviewId: string
- language: string
- timestamp: datetime
- text: string (full body)
- recommendation: bool (up/down)
- helpfulVotes: int
- sourceUrl: string
- user: omitted/stripped (PII)

### StorePage (Bronze)
- appId: int
- name: string
- releaseDate: string/raw
- genres/tags: string[]
- developers/publishers: string[]
- platforms: string[]
- languages: string[] (raw)
- type: string (game, dlc, demo, soundtrack, tool, workshop)
- sourceUrl: string
- recommendations: { total: int, positive: int, negative: int } (enables Bronze candidacy evaluation)

### NewsItem (Bronze)
- appId: int
- newsId: string
- title: string
- timestamp: datetime
- bodyRaw: string
- sourceUrl: string
  
### NewsItemRefined (Silver)
- appId: int
- newsId: string
- title: string
- timestamp: date (UTC)
- bodyClean: string (sanitized from HTML/BBCode)
- newsClass: enum [patch_update, marketing_other] (initial coarse taxonomy via discovery census of tags/body features; may evolve)
- isPatchNotes: bool (via tags=patchnotes or classifier)

### RefinedGame (Silver)
- appId: int
- canonicalName: string
- releaseDate: date (normalized)
- genres: string[] (standardized)
- developers: string[] (standardized)
- platforms: enum[] [win, mac, linux]
- languages: string[] (normalized ISO codes)
- type: enum [game, dlc, demo, soundtrack, tool, workshop]
- parentGameId: int|null (for DLC/demos linking to base game)
- gameClusterId: int|null (for grouping up to 100 related appids per true game)
- reviewCounts: { total, recentWindow }
- reviewStats: { upRatio, helpfulRate }
- dupGroupId: string|null (potential duplicates grouping)
 - patchNotesRatio: float (0..1)
 - devResponseRate: float (0..1)
 - avgDevResponseTimeHours: float
 - reviewUpdateVelocity: float (updates/day in window)
 - ugcMetrics?: { velocityPerMonth: float, maintenanceRate6m: float, uniqueAuthors: int }
 - reviewAggs: {
		positivity_rating: float,
		gmean_word_count: float,
		gmean_unique_word_count: float,
		gmean_resonance_score: float,
		gmean_hours_played: float,
		gmean_num_games_owned: float,
		gmean_author_num_reviews: float,
		first_review_date: date,
		last_review_date: date,
		inferred_release_year: int
	}
 - reviewFilterApplied: { minUniqueWords: int, receivedForFree: bool }

### Candidate (Gold)
- appId: int
- included: bool
- reasons: string[] (policy keys)
- metrics: { totalReviews, recentReviews, upRatio, activityScore }
- sources: string[] (paths/urls for evidence)
- policyId: string
 - embedding?: float[] (game-level vector; derived from weighted review embeddings ⊕ metadata embedding)
 - reviewCapApplied?: int (actual cap used when promoting to Gold, e.g., 200)

### Manifest
- dataset: enum [bronze, silver, gold]
- partition: string
- runId: string
- createdAt: datetime
- files: { path, rows, bytes, checksum }[]
- lineage: { sources: string[], transforms: string[] }

### PolicyMetadata (attached to Run summary and Manifest)
- policy_version: string (semver)
- thresholds: { recommendations_min: int, review_count_min: int }
- fallback_reason: enum [review_count_fallback, none]
- effective_values: { recommendations_min: int, review_count_min: int } (after overrides)
- sample_counts: { total_apps: int, included_by_recommendations: int, included_by_fallback: int, excluded_missing: int }
- notes?: string

## Relationships
- Run 1-* RawArtifact
- RawArtifact -> RefinedGame (via transforms)
- RefinedGame -> Candidate (policy evaluation)

## Validation Rules
- Bronze candidacy requires ≥10 total recommendations (positive + negative combined) as determined from store page metadata.
- Bronze review cap per app enforced at write time (default 10).
- Game clusters limited to 100 appids per true game (1 base game + up to 99 DLC/demos/soundtracks).
- No PII stored in Review objects (user fields stripped).
- Timestamps normalized to UTC in Silver.
