# ExpressRecipe

## What This Is

ExpressRecipe is a local-first, cloud-capable dietary management platform that helps individuals with dietary restrictions (medical, religious, health-related, or personal) manage food choices, meal planning, inventory, and shopping. The platform provides intelligent recommendations, real-time allergen alerts, and community-driven product data—all while working fully offline with optional cloud sync.

**Current Status**: Production-quality backend (80% complete), functional frontend (60% complete), ready for system testing and final integration work.

## Core Value

**Safe dietary management that works offline.** Users must be able to scan products, check allergen compatibility, manage inventory, and access their recipes without an internet connection. Cloud sync enhances but never blocks core functionality.

## Requirements

### Validated

<!-- Shipped, built, and verified working in codebase. -->

- ✓ **15 microservices** architecture with Aspire orchestration — existing
- ✓ **User authentication** with JWT, refresh tokens, password hashing — existing
- ✓ **User profiles** with allergens, dietary restrictions, family members — existing
- ✓ **Product management** with full CRUD, allergen tracking, OpenFoodFacts import — existing
- ✓ **Recipe management** with multiple parsers (YouTube, MasterCook, Paprika, JSON, PlainText) — existing
- ✓ **Recipe scaling** by serving size with automatic conversion — existing
- ✓ **Per-family-member ratings** with half-star support (0.0-5.0 scale) — existing
- ✓ **Shopping list generation** from recipes with ingredient linking — existing
- ✓ **Inventory tracking** with expiration monitoring and usage history — existing
- ✓ **Household/family system** with role-based permissions (Owner/Admin/Member/Viewer) — existing
- ✓ **GPS integration** with Haversine formula for store/address distance calculations — existing
- ✓ **Lock mode barcode scanning** with Add/Use/Dispose sessions — existing
- ✓ **Automatic allergen discovery** from disposed items with pattern recognition — existing
- ✓ **Multi-store price comparison** with deal tracking (BOGO, Sales) — existing
- ✓ **Advanced search/filtering** by category, cuisine, ingredient, prep/cook time — existing
- ✓ **Database migrations** (77 total across all services) — existing
- ✓ **ADO.NET data access** with SqlHelper base class — existing
- ✓ **CQRS pattern** for complex operations — existing
- ✓ **Docker Compose** setup for infrastructure — existing

### Active

<!-- Current scope being built toward for v1.0 completion. -->

- [ ] **System testing** - Run 6 critical path tests defined in SYSTEM_TEST_GUIDE.md
  - [ ] User registration & authentication end-to-end
  - [ ] Household creation & GPS detection
  - [ ] Barcode lock mode scanning (Add/Use/Dispose)
  - [ ] Recipe import & shopping list generation
  - [ ] Multi-store price comparison with GPS
  - [ ] Per-family-member recipe ratings
- [ ] **Inventory/Shopping UI pages** - Build list, add, edit views for frontend
- [ ] **Performance optimization** - API response times < 500ms (p95)
- [ ] **Integration testing** - Verify cross-service calls (Auth→User, Recipe→Shopping, etc.)
- [ ] **GPS features testing** - Verify Haversine calculations, nearby stores (10km radius)
- [ ] **Production readiness** - Security review, error handling, logging, monitoring
- [ ] **Blazor UI polish** - Complete remaining placeholder pages, test runtime behavior
- [ ] **Email verification flow** - Test end-to-end user email verification
- [ ] **Password reset flow** - Implement and test password reset via email
- [ ] **Deployment configuration** - Azure Container Apps setup, CI/CD pipelines

### Out of Scope

<!-- Explicit boundaries to prevent scope creep. -->

- **MAUI mobile apps** — Deferred per explicit user direction until web app complete
- **Comprehensive test suite** — Deferred per explicit user direction (manual testing first)
- **OAuth providers** (Google, GitHub, etc.) — Structure exists, implementation deferred to v2
- **Phase 5 features** — Multi-region deployment, iOS app, internationalization, enterprise features, integrations (grocery delivery, smart home)
- **AI-powered meal planning** — Deferred to v2, focus on core functionality first
- **Real-time collaboration** — Deferred to v2, async sync sufficient for v1
- **Advanced analytics** — Deferred to v2, basic reports only in v1

## Context

**Project Type**: Brownfield — Extensive existing codebase with 80-90% backend completion

**Recent Work** (Feb 2026):
- Fixed RecipeService build error (type conversion for RecipeTagDto)
- Merged inventory/shopping branch (+11,637 LOC with household, GPS, lock mode features)
- Restored full Aspire AppHost configuration (15 services, SQL Server, Redis, RabbitMQ)
- Created comprehensive system test guide with 6 critical path tests

**Codebase Stats**:
- 10,831+ lines of controller code across 15 microservices
- 77 SQL migrations implemented
- 50+ Blazor components
- Recent additions: ~17,000 LOC from two major feature branches

**Architecture**:
- Microservices with bounded contexts (Auth, User, Product, Recipe, Inventory, Shopping, Scanner, MealPlanning, Price, Recall, Notification, Community, Sync, Search, Analytics)
- .NET Aspire for cloud-native orchestration
- Local-first design (SQLite for offline, SQL Server for cloud)
- Event-driven communication (RabbitMQ/Azure Service Bus)
- ADO.NET for performance (no Entity Framework)

**Key Features Already Working**:
- User registration with cross-service profile creation
- JWT authentication with refresh token rotation
- Product database with OpenFoodFacts import (1,387-line service)
- Recipe parsers for multiple formats (YouTube, MasterCook, Paprika, MealMaster, JSON, Web)
- Household/family system with multi-address GPS support
- Lock mode barcode scanning with allergen discovery
- Shopping list templates, favorites, price comparison
- Per-family-member recipe ratings with SQL triggers for aggregation

## Constraints

- **Tech Stack**: .NET 10, Aspire, Blazor, WinUI 3 — Cannot change core framework
- **Data Access**: ADO.NET only (no EF Core) — Performance requirement, already implemented
- **Platform**: Windows development environment — Primary target platform
- **Database**: SQL Server (cloud), SQLite (local) — Already configured with 77 migrations
- **Timeline**: Complete v1.0 testing and deployment — Finish the remaining 10-20% of work
- **Storage**: `I:\DockerVolumes\ExpressRecipe` for persistence — Already configured in Aspire
- **Ignore List**: MAUI builds, unit/integration tests — Explicit user direction to defer

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| **Local-first architecture** | Users must work offline for safety-critical allergen checks | ✓ Good — Core value delivery |
| **ADO.NET over EF Core** | Maximum performance and control, explicit SQL | ✓ Good — 10,831 lines of production code |
| **Microservices (15 services)** | Bounded contexts, independent scaling, maintainability | ✓ Good — Clean separation of concerns |
| **Aspire orchestration** | Development environment coordination, service discovery, observability | ✓ Good — Simplified local development |
| **Per-family-member ratings** | Different family members have different food preferences | ✓ Good — Unique feature, well-implemented |
| **Lock mode scanning** | Rapid barcode entry without navigation (Add/Use/Dispose modes) | ✓ Good — Power user efficiency |
| **GPS-based features** | Haversine formula for store distance, auto-detect addresses | ✓ Good — Practical feature for shopping |
| **Automatic allergen discovery** | Pattern recognition from disposed items helps identify hidden allergens | ✓ Good — Safety-focused innovation |
| **Household/family system** | Multi-household support with role-based permissions | ✓ Good — Enables family use cases |
| **Defer MAUI/tests** | Focus on completing backend/frontend integration first | — Pending — Will revisit after v1.0 |
| **Standard planning depth** | Balance between thorough coverage and shipping velocity | — Pending — Just configured |
| **Auto-advance pipeline** | Chain discuss → plan → execute stages automatically | — Pending — Just enabled |
| **Per-milestone branching** | Create branch for entire milestone (gsd/{version}-{name}) | — Pending — Just configured |

---
*Last updated: 2026-02-16 after GSD initialization (auto mode, standard depth)*
