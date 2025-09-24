# Data Model

Date: 2025‑09‑21 | Branch: `002-we-intend-to`

## Entities

### Game
- id: string (partition key)
- title: string
- slug: string
- releaseDate: date (ISO 8601)
- adultOnly: boolean
- controllerSupport: enum("none","partial","full")
- tags: string[] (lower_snake_case)
- priceMicros: number
- currency: string (ISO 4217)
- platforms: string[] (enum values)
- reviewCount: number
- purchaseOriginRatio: object { steam: number, other: number, unknown: number, totalSampled: number }
- vector: float[768] (optional projected field or separate collection)

### Review
- id: string
- gameId: string (partition key)
- authorId: string
- createdAt: date (ISO 8601)
- helpfulVotes: number
- language: string (BCP 47)
- content: string (normalized)
- contentHash: string (ETL dedup aid)
- sentiment: number [-1..1]
- vector: float[768]
- source: object { provider: "steam", url: string, fetchedAt: date }

### Candidate
- gameId: string
- gameTitle: string
- reviewId?: string
- textScore: number
- semanticScore: number
- combinedScore: number (default 0.5*semantic + 0.5*text)
- excerpt?: string
- reviewMeta?: { helpfulVotes: number, createdAt: date }

### FilterSet
- query: string
- minReviews?: number
- adultOnly?: boolean
- controller?: "none"|"partial"|"full"
- includeDLC?: boolean
- candidateCap: number (200–2000)
- purchaseOriginMaxSample: number (default 10000)
- convergence?: { minReviewMatches?: number, requireGameAndReview?: boolean }

### ReRankWeights
- semantic: number (0..1)
- text: number (0..1)

### QuerySession
- id: string
- startedAt: date
- filters: FilterSet
- serverParams: { vectorK: number, textK: number, weights: ReRankWeights }

## Collections (Cosmos NoSQL)
- games (partitionKey: `/id`)
- reviews (partitionKey: `/gameId`, vector index on `vector` with DiskANN)
- Option: materialized view `game_vectors` if needed for game‑level matching

## Indexing
- Text: content/title/tags; keep projections small
- Vector: 768‑dim DiskANN, tuned for efConstruction, maxDegree; query efSearch adjustable

## Invariants
- Reviews per game sampled ≤ 10,000 for ratio metrics; working set for relevance smaller, ordered by helpfulness/recency
- All dates UTC ISO 8601; language BCP 47; currency ISO 4217

