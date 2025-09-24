
# Implementation Plan: Actual Game Search (Hybrid Full-Text + Semantic)

**Branch**: `002-we-intend-to` | **Date**: 2025-09-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-we-intend-to/spec.md`

## Execution Flow (/plan command scope)
```
1. Load feature spec from Input path
   → If not found: ERROR "No feature spec at {path}"
2. Fill Technical Context (scan for NEEDS CLARIFICATION)
   → Detect Project Type from context (web=frontend+backend, mobile=app+api)
   → Set Structure Decision based on project type
3. Fill the Constitution Check section based on the content of the constitution document.
4. Evaluate Constitution Check section below
   → If violations exist: Document in Complexity Tracking
   → If no justification possible: ERROR "Simplify approach first"
   → Update Progress Tracking: Initial Constitution Check
5. Execute Phase 0 → research.md
   → If NEEDS CLARIFICATION remain: ERROR "Resolve unknowns"
6. Execute Phase 1 → contracts, data-model.md, quickstart.md, agent-specific template file (e.g., `CLAUDE.md` for Claude Code, `.github/copilot-instructions.md` for GitHub Copilot, `GEMINI.md` for Gemini CLI, `QWEN.md` for Qwen Code or `AGENTS.md` for opencode).
7. Re-evaluate Constitution Check section
   → If new violations: Refactor design, return to Phase 1
   → Update Progress Tracking: Post-Design Constitution Check
8. Plan Phase 2 → Describe task generation approach (DO NOT create tasks.md)
9. STOP - Ready for /tasks command
```

**IMPORTANT**: The /plan command STOPS at step 7. Phases 2-4 are executed by other commands:
- Phase 2: /tasks command creates tasks.md
- Phase 3-4: Implementation execution (manual or via tools)

## Summary
Ultra-low-cost, ultra-high-accuracy hybrid fulltext/semantic search over Steam games and reviews. Visitors input natural language descriptions of their "dream game" and receive ranked candidates with client-side re-ranking controls. System supports convergence filtering, evidence snippets, and deterministic fallback handling. Built as independently-improvable components with static frontend, API backend, ETL worker, and vector-enabled database.

## Technical Context
**Language/Version**: .NET 10 (C#)  
**Primary Dependencies**: .NET Aspire 10, Microsoft.Extensions.AI, Azure Cosmos DB SDK, Ollama (via HTTP)  
**Storage**: Azure Cosmos DB NoSQL with DiskANN vector indexing  
**Testing**: xUnit, contract tests, integration tests with Cosmos emulator  
**Target Platform**: Linux containers (Aspire-orchestrated for local dev)  
**Project Type**: Web application (API backend + static frontend)  
**Performance Goals**: Quality-first approach, async acceptable for expensive operations (<5s search latency)  
**Constraints**: <200ms API response for cheap preview paths, 60 req/min rate limiting, WCAG 2.1 AA compliance  
**Scale/Scope**: Proof-of-concept scale (~50k games, ~10M reviews), template for generic product search

**Enhanced Context from Development History:**
- Aspire AppHost manages Cosmos emulator, Ollama resource, service discovery
- Result<T> envelope pattern for all API responses
- HybridRanker with configurable semantic/textual weights (default 50/50)
- VectorDistance adaptive arity handling (2-arg/3-arg compatibility)  
- ETL pipeline with Steam API integration, review sampling (up to 200/game)
- In-memory fallback repositories for test environments
- User-friendly error codes catalog approach (format: "Problem (EXXX). Action.")
- Context-sensitive terminology: "title" for domain objects, "result" for API responses

## Implementation Approach
**Primary Strategy**: Domain-driven decomposition with Result<T> envelope pattern and configurable ranking algorithms.

**Core Components**:
1. **ActualGameSearch.Core** - Domain layer with models, Result<T>, ranking logic, repository abstractions
2. **ActualGameSearch.Api** - Minimal API with JSON envelope responses, static frontend serving
3. **ActualGameSearch.Worker** - ETL pipeline for Steam API ingestion and vector embedding generation  
4. **ActualGameSearch.AppHost** - Aspire orchestration for local development (Cosmos emulator + Ollama)
5. **ActualGameSearch.ServiceDefaults** - Cross-cutting concerns (observability, configuration)

**Data Flow**:
1. Steam API → Worker ETL → Cosmos DB (documents + vectors)
2. Search query → API → Core services → HybridRanker → Results
3. Client re-ranking via deterministic parameters

**Key Architectural Decisions** (from development history):
- **Result<T> everywhere**: No exceptions for control flow, predictable error handling
- **Hybrid ranking with fallbacks**: TextualRanker for missing embeddings, HybridRanker for optimal cases
- **Adaptive vector compatibility**: VectorDistance handles both 2-arg and 3-arg scenarios
- **Repository pattern**: IGameRepository/IReviewRepository with Cosmos and in-memory implementations
- **Deterministic client control**: User adjusts weights, algorithm parameters without server roundtrips
- **Evidence-based results**: Review snippets with source attribution for transparency
- **Rate limiting**: 60 req/min with user-friendly error messages
- **Graceful degradation**: In-memory repositories when external dependencies unavailable

## Constitution Check
*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Principle I - Radical Simplicity**: ✅ PASS
- Single feature scope (hybrid search), minimal surface area
- Static frontend + API backend, no complex UI frameworks
- Result<T> envelope eliminates exception handling complexity

**Principle II - Evidence-Based Decision Making**: ✅ PASS  
- Architecture decisions based on 4 days of actual development
- Performance characteristics validated through real Ollama embeddings
- Error handling patterns evolved from actual Steam API edge cases

**Principle III - Quality Over Quantity**: ✅ PASS
- Quality-first approach with <5s acceptable search latency
- Comprehensive test coverage (unit, integration, contract)
- Error surfacing philosophy: "expose errors, do not mask them"

**Principle IV - Transparency & Documentation**: ✅ PASS
- Extensive conversation history and provenance tracking
- OpenAPI contracts for all endpoints
- User-friendly error codes with actionable messages

**Principle V - Sustainable Development**: ✅ PASS
- .NET 10 Aspire provides production-ready orchestration
- Cosmos DB vector indexing scales beyond proof-of-concept
- Template-first approach supports generic product search reuse

**Principle VI - Evidence-Based Documentation & Provenance**: ✅ PASS
- All architectural decisions traceable to specific conversation turns
- Reality Check sections align spec with implemented behavior
- Fact-checked interleaved timelines provide implementation history

## Project Structure

### Documentation (this feature)
```
specs/[###-feature]/
├── plan.md              # This file (/plan command output)
├── research.md          # Phase 0 output (/plan command)
├── data-model.md        # Phase 1 output (/plan command)
├── quickstart.md        # Phase 1 output (/plan command)
├── contracts/           # Phase 1 output (/plan command)
└── tasks.md             # Phase 2 output (/tasks command - NOT created by /plan)
```

### Source Code (repository root)
```
# Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure]
```

**Structure Decision**: [DEFAULT to Option 1 unless Technical Context indicates web/mobile app]

## Phase 0: Outline & Research
**Status**: ✅ COMPLETED (Days 1-3 development)

**Research Findings** (from conversation history):

**1. .NET Aspire + Cosmos DB Vector Integration**:
- **Decision**: Azure Cosmos DB NoSQL with DiskANN vector indexing  
- **Rationale**: Native vector support, proven scalability, Aspire integration
- **Implementation**: Gateway mode + HTTP/1.1 for emulator compatibility
- **Evidence**: Successful vector queries with adaptive VectorDistance arity handling

**2. Ollama Embeddings via Microsoft.Extensions.AI**:
- **Decision**: Ollama managed as Aspire resource with nomic-embed-text:v1.5
- **Rationale**: Local development, no API costs, 768-dimension vectors
- **Implementation**: Service discovery via environment variables, not hardcoded endpoints
- **Evidence**: Validated real embeddings (>500ms latency vs <10ms synthetic fallbacks)

**3. Steam API Integration Patterns**:
- **Decision**: Public appdetails + appreviews endpoints, no API key required  
- **Rationale**: Sufficient data for proof-of-concept, avoids rate limiting complexity
- **Implementation**: language=all for reviews, up to 200 reviews per game
- **Evidence**: Successfully ingested authentic multilingual review data

**4. Error Handling Philosophy**:
- **Decision**: Surface errors explicitly, user-friendly codes (E001, E002, etc.)
- **Rationale**: "Expose errors, do not mask them" - better than silent failures
- **Implementation**: Result<T> envelope with structured error messages
- **Evidence**: Reduced debugging time, clear failure modes for malformed Steam data

**5. Hybrid Ranking Algorithm**:
- **Decision**: Configurable semantic/textual weights with deterministic fallbacks
- **Rationale**: User control over ranking, graceful degradation when embeddings unavailable
- **Implementation**: HybridRanker with 50/50 default weights, TextualRanker fallback
- **Evidence**: Test coverage for multiple weight combinations and fallback scenarios

**All NEEDS CLARIFICATION resolved**: ✅ No remaining unknowns

## Phase 1: Design & Contracts  
**Status**: ✅ COMPLETED (Days 1-3 development)

**1. Data Model Implementation**:
- **Core Models**: GameSummary, GameCandidates, Candidate, SearchResponse
- **DTOs**: Steam API models with polymorphic field handling (weighted_vote_score)
- **Primitives**: Result<T> envelope pattern for consistent error handling
- **Evidence**: Located in `src/ActualGameSearch.Core/Models/` with comprehensive test coverage

**2. API Contracts Generated**:
- ✅ `GET /api/search/games?q={query}&top={limit}` - Game-focused search
- ✅ `GET /api/search/reviews?q={query}&top={limit}&fields=full` - Review-centric search  
- ✅ `GET /api/search?q={query}&top={limit}&fields=full` - Combined search results
- **Format**: All responses use Result<T> envelope: `{ok: bool, data: T|null, error: string|null}`
- **Evidence**: OpenAPI specification at `specs/002-we-intend-to/contracts/openapi.yaml`

**3. Contract Tests Implementation**:
- ✅ `ActualGameSearch.ContractTests` - API endpoint validation
- ✅ `GamesSearchContractTests` - Games search endpoint compliance
- ✅ `ReviewsSearchContractTests` - Reviews search endpoint compliance  
- **Evidence**: All contract tests passing, validate Result<T> envelope format

**4. Integration Test Scenarios**:
- ✅ `CheapPreviewFlowTests` - Fast response path validation (<200ms)
- ✅ `ConvergenceTests` - Ranking algorithm validation with different weights
- **Evidence**: Located in `tests/ActualGameSearch.IntegrationTests/`

**5. Repository Abstractions**:
- ✅ `IGameRepository` / `IReviewRepository` interfaces
- ✅ `CosmosGamesRepository` / `CosmosReviewsRepository` implementations  
- ✅ `InMemoryRepositories` for test environments
- **Evidence**: Cosmos and in-memory implementations both tested and working

**6. Static Frontend Implementation**:
- ✅ `wwwroot/index.html` - Landing page
- ✅ `wwwroot/search.html` - Search interface for manual testing
- ✅ `wwwroot/app.js` - Client-side re-ranking logic
- **Evidence**: Manual search validation successful through UI

**Output Artifacts**:
- ✅ `data-model.md` - Entity definitions and relationships
- ✅ `contracts/openapi.yaml` - Complete API specification  
- ✅ `quickstart.md` - Setup and validation steps
- ✅ Failing tests converted to passing tests through TDD process

## Phase 2: Task Planning Approach
**Status**: ✅ COMPLETED (via conversation history)

**Actual Task Execution Pattern** (from development history):
- **Test-Driven Development**: Contract tests → Unit tests → Integration tests → Implementation
- **Component-wise Development**: Core models → Repositories → Services → API endpoints → Frontend
- **Infrastructure-first**: Aspire orchestration → Cosmos bootstrap → Ollama integration → ETL pipeline

**Key Implementation Sequences** (achieved):
1. **T011-T015**: Core architecture (Result<T>, models, ranking, embeddings, Cosmos integration)
2. **ETL Pipeline**: Steam API integration, review sampling, vector generation, upsert logic
3. **Search Endpoints**: Games/reviews/combined search with hybrid ranking
4. **Error Handling**: User-friendly codes, explicit failure surfacing, adaptive fallbacks
5. **Quality Gates**: Schema validation, error surfacing, authentic data requirements

**Parallel Execution Achieved**: ✅ Repository implementations, test suites, API endpoints developed in parallel

**Output**: Comprehensive task execution across 4 days of development with continuous validation

## Phase 3+: Implementation Status
**Phase 3**: ✅ COMPLETED - All core functionality implemented
**Phase 4**: ✅ COMPLETED - Full end-to-end validation successful  
**Phase 5**: ✅ COMPLETED - Performance validation, error handling, quality gates established

**Current System Status**:
- ✅ Hybrid semantic/textual search operational
- ✅ Steam API integration with authentic data ingestion
- ✅ Ollama embeddings via Aspire resource management
- ✅ Cosmos DB vector indexing with adaptive query handling
- ✅ Rate limiting (60 req/min) and error handling implemented
- ✅ Static frontend with client-side re-ranking
- ✅ Comprehensive test coverage (unit, integration, contract)
- ✅ WCAG 2.1 AA compliance for responsive UI

**Evidence**: All endpoints functional, tests passing, manual validation successful

## Complexity Tracking
**No Constitutional Violations**: All complexity justified by evidence-based development

**Design Decisions Validated**:
- **Repository Pattern**: Justified by need for Cosmos + in-memory test implementations
- **Hybrid Ranking**: Justified by user control requirements and fallback scenarios  
- **Result<T> Envelope**: Justified by error handling consistency and client expectations
- **Aspire Orchestration**: Justified by development experience improvements and production readiness

## Progress Tracking
**Phase Status**:
- [x] Phase 0: Research complete ✅ (Days 1-3 development)
- [x] Phase 1: Design complete ✅ (Days 1-3 development)  
- [x] Phase 2: Task planning complete ✅ (via development history)
- [x] Phase 3: Tasks generated ✅ (executed through TDD process)
- [x] Phase 4: Implementation complete ✅ (All core functionality operational)
- [x] Phase 5: Validation passed ✅ (End-to-end testing successful)

**Gate Status**:
- [x] Initial Constitution Check: PASS ✅
- [x] Post-Design Constitution Check: PASS ✅  
- [x] All NEEDS CLARIFICATION resolved ✅
- [x] Complexity deviations documented ✅ (None - all justified)

**Current Readiness**: ✅ PRODUCTION-READY for proof-of-concept deployment

**Next Steps**: 
- Deploy to production environment (actualgamesearch.com)
- Monitor search quality and performance metrics
- Iterative improvements based on user feedback

---
*Based on Constitution v2.1.1 - See `/memory/constitution.md`*
