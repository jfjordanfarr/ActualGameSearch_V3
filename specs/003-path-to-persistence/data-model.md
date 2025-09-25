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
- reviewCounts: { total, recentWindow }
- reviewStats: { upRatio, helpfulRate }
- dupGroupId: string|null (potential duplicates grouping)
 - patchNotesRatio: float (0..1)
 - devResponseRate: float (0..1)
 - avgDevResponseTimeHours: float
 - reviewUpdateVelocity: float (updates/day in window)
 - ugcMetrics?: { velocityPerMonth: float, maintenanceRate6m: float, uniqueAuthors: int }

### Candidate (Gold)
- appId: int
- included: bool
- reasons: string[] (policy keys)
- metrics: { totalReviews, recentReviews, upRatio, activityScore }
- sources: string[] (paths/urls for evidence)
- policyId: string

### Manifest
- dataset: enum [bronze, silver, gold]
- partition: string
- runId: string
- createdAt: datetime
- files: { path, rows, bytes, checksum }[]
- lineage: { sources: string[], transforms: string[] }

## Relationships
- Run 1-* RawArtifact
- RawArtifact -> RefinedGame (via transforms)
- RefinedGame -> Candidate (policy evaluation)

## Validation Rules
- Bronze review cap per app enforced at write time (default 10).
- No PII stored in Review objects (user fields stripped).
- Timestamps normalized to UTC in Silver.
