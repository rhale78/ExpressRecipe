# ExpressRecipe Backend Services - Complete Overview

**Last Updated:** 2025-11-19
**Status:** All 16 microservices implemented and containerized

---

## Infrastructure Services (4)

| Service | Container | Port | Purpose |
|---------|-----------|------|---------|
| **SQL Server 2022** | expressrecipe-sqlserver | 1433 | Primary database for all services |
| **Redis** | expressrecipe-redis | 6379 | Caching layer and session storage |
| **RabbitMQ** | expressrecipe-rabbitmq | 5672, 15672 | Message broker for async communication |
| **Ollama** | expressrecipe-ollama | 11434 | Local AI engine (llama2, mistral, codellama) |

---

## Core Microservices (7)

### 1. Auth Service (Port 5000)
**Purpose:** Authentication and authorization
**Endpoints:**
- `POST /api/auth/register` - User registration
- `POST /api/auth/login` - User login (JWT tokens)
- `POST /api/auth/refresh` - Refresh access token
- `POST /api/auth/logout` - Invalidate token
- `GET /api/auth/validate` - Validate token

**Database Tables:** User, RefreshToken, UserRole
**Features:** JWT tokens, refresh tokens, role-based access control

---

### 2. User Service (Port 5001)
**Purpose:** User profiles and preferences management
**Endpoints:**
- `GET /api/users/profile` - Get user profile
- `PUT /api/users/profile` - Update profile
- `POST /api/users/allergens` - Add allergen
- `DELETE /api/users/allergens/{id}` - Remove allergen
- `GET /api/users/dietary-preferences` - Get preferences
- `PUT /api/users/dietary-preferences` - Update preferences

**Database Tables:** UserProfile, UserAllergen, DietaryPreference
**Features:** Allergen tracking, dietary restrictions, health goals

---

### 3. Product Service (Port 5002)
**Purpose:** Product database and barcode lookup
**Endpoints:**
- `GET /api/products` - Search products
- `GET /api/products/{id}` - Get product details
- `POST /api/products` - Create product
- `PUT /api/products/{id}` - Update product
- `GET /api/products/barcode/{upc}` - Lookup by barcode
- `POST /api/products/batch` - Batch create/update

**Database Tables:** Product, ProductIngredient, ProductAllergen, ProductNutrition
**Features:** UPC/EAN lookup, ingredient lists, allergen information, nutrition facts

---

### 4. Recipe Service (Port 5003)
**Purpose:** Recipe management and discovery
**Endpoints:**
- `GET /api/recipes` - Browse recipes
- `GET /api/recipes/{id}` - Get recipe details
- `POST /api/recipes` - Create recipe
- `PUT /api/recipes/{id}` - Update recipe
- `DELETE /api/recipes/{id}` - Delete recipe
- `GET /api/recipes/search` - Search recipes
- `POST /api/recipes/{id}/save` - Save to favorites

**Database Tables:** Recipe, RecipeIngredient, RecipeStep, RecipeTag, SavedRecipe
**Features:** Recipe CRUD, search, filtering by allergens/diet, favorites

---

### 5. Inventory Service (Port 5004)
**Purpose:** Pantry and refrigerator inventory tracking
**Endpoints:**
- `GET /api/inventory` - Get all inventory items
- `POST /api/inventory` - Add item
- `PUT /api/inventory/{id}` - Update item
- `DELETE /api/inventory/{id}` - Remove item
- `GET /api/inventory/expiring` - Get expiring items
- `GET /api/inventory/low-stock` - Get low stock items

**Database Tables:** InventoryItem, InventoryLocation, StockAlert
**Features:** Expiration tracking, quantity management, low stock alerts

---

### 6. Shopping Service (Port 5005)
**Purpose:** Shopping list management
**Endpoints:**
- `GET /api/shopping` - Get shopping lists
- `POST /api/shopping` - Create list
- `POST /api/shopping/{id}/items` - Add item
- `PUT /api/shopping/items/{id}` - Update item
- `DELETE /api/shopping/items/{id}` - Remove item
- `POST /api/shopping/{id}/check-prices` - Get price comparison

**Database Tables:** ShoppingList, ShoppingListItem, Store, StorePrice
**Features:** Multiple lists, item organization, price comparison

---

### 7. Meal Planning Service (Port 5006)
**Purpose:** Weekly meal planning and recipe scheduling
**Endpoints:**
- `GET /api/meal-plans` - Get meal plans
- `POST /api/meal-plans` - Create meal plan
- `PUT /api/meal-plans/{id}` - Update meal plan
- `POST /api/meal-plans/{id}/meals` - Add meal
- `POST /api/meal-plans/generate` - Auto-generate plan
- `POST /api/meal-plans/{id}/to-shopping-list` - Create shopping list

**Database Tables:** MealPlan, PlannedMeal, MealPlanRecipe
**Features:** Weekly planning, recipe scheduling, shopping list generation

---

## Supporting Services (4)

### 8. Scanner Service (Port 5007)
**Purpose:** Barcode scanning and allergen detection
**Endpoints:**
- `POST /api/scanner/scan` - Scan barcode
- `POST /api/scanner/scan/{userId}` - Scan with user profile
- `GET /api/scanner/history` - Get scan history
- `POST /api/scanner/report-missing` - Report missing product

**Database Tables:** ScanHistory, BarcodeMapping, MissingProductReport
**Features:** UPC-A/EAN-13 support, instant allergen alerts, history tracking

**Integration:** Connects to Product Service and User Service for cross-referencing

---

### 9. Recall Service (Port 5008)
**Purpose:** FDA/USDA recall monitoring and alerts
**Endpoints:**
- `GET /api/recalls/active` - Get active recalls
- `POST /api/recalls/search` - Search recalls
- `GET /api/recalls/{id}` - Get recall details
- `GET /api/recalls/notifications` - Get user notifications
- `POST /api/recalls/check-inventory` - Check user inventory
- `POST /api/recalls/subscriptions` - Create subscription

**Database Tables:** Recall, RecallProduct, UserRecallNotification, RecallSubscription
**Features:** FDA/USDA API integration, inventory cross-check, email/push notifications

**Background Workers:**
- RecallMonitorWorker - Polls FDA/USDA APIs every hour
- FDARecallImportService - Imports and processes recall data

---

### 10. Search Service (Port 5009)
**Purpose:** Cross-service global search
**Endpoints:**
- `POST /api/search` - Global search
- `POST /api/search/advanced` - Advanced search with filters
- `GET /api/search/suggestions` - Get search suggestions
- `GET /api/search/recent` - Get recent searches
- `DELETE /api/search/recent` - Clear history

**Database Tables:** SearchIndex, SearchHistory, SearchSuggestion
**Features:** Full-text search across Products, Recipes, Ingredients

**Indexed Entities:**
- Products (name, brand, ingredients, allergens)
- Recipes (title, description, ingredients)
- Ingredients (name, alternatives)

---

### 11. Sync Service (Port 5010)
**Purpose:** Cloud synchronization and conflict resolution
**Endpoints:**
- `GET /api/sync/status` - Get sync status
- `POST /api/sync/trigger` - Manual sync
- `POST /api/sync/entity/{type}/{id}` - Sync specific entity
- `GET /api/sync/conflicts` - Get conflicts
- `POST /api/sync/conflicts/resolve` - Resolve conflict
- `GET /api/sync/settings` - Get sync settings
- `PUT /api/sync/settings` - Update settings

**Database Tables:** SyncQueue, SyncConflict, SyncLog, SyncSettings
**Features:** Auto-sync, conflict detection, manual resolution, sync queue

**Sync Strategies:**
- Last-Write-Wins
- User-Prompted Resolution
- Automatic Merge (for non-conflicting fields)

---

## Advanced Services (5)

### 12. AI Service (Port 5100)
**Purpose:** AI-powered recipe suggestions and meal planning
**Endpoints:**
- `POST /api/ai/recipes/suggest` - Get recipe suggestions
- `POST /api/ai/ingredients/substitute` - Get ingredient substitutions
- `POST /api/ai/recipes/extract` - Extract recipe from text
- `POST /api/ai/meal-plans/suggest` - Generate meal plan
- `POST /api/ai/allergens/detect` - Detect allergens
- `POST /api/ai/chat` - AI chat assistant

**Technology:** Ollama with llama2, mistral, codellama models
**Features:**
- Recipe suggestions based on available ingredients
- Allergen-aware ingredient substitutions
- Recipe extraction from text/images/URLs
- Personalized meal planning
- Natural language chat interface

---

### 13. Notification Service (Port 5101)
**Purpose:** Real-time push notifications
**Endpoints:**
- `POST /api/notifications` - Create notification
- `GET /api/notifications` - Get user notifications
- `PUT /api/notifications/{id}/read` - Mark as read
- `PUT /api/notifications/mark-all-read` - Mark all read
- `GET /api/notifications/preferences` - Get preferences
- `PUT /api/notifications/preferences` - Update preferences

**Database Tables:** Notification, NotificationPreference, NotificationTemplate, DeliveryLog
**Features:**
- SignalR for real-time delivery
- Email, Push, SMS, In-app notifications
- Template system
- Delivery tracking and retry logic

**SignalR Hub:** `/hubs/notifications`

---

### 14. Analytics Service (Port 5102)
**Purpose:** Usage analytics and reporting
**Endpoints:**
- `POST /api/analytics/track` - Track event
- `GET /api/analytics/dashboard` - Get dashboard data
- `GET /api/analytics/reports/usage` - Usage report
- `GET /api/analytics/reports/health` - Health metrics
- `GET /api/analytics/reports/savings` - Cost savings
- `GET /api/analytics/insights` - Personalized insights

**Database Tables:** Event, DashboardMetric, Report, UserInsight
**Features:**
- Real-time event tracking
- Usage statistics
- Cost savings calculations
- Food waste tracking
- Personalized recommendations

---

### 15. Community Service (Port 5103)
**Purpose:** Recipe ratings, reviews, and community features
**Endpoints:**
- `POST /api/community/recipes/{id}/rate` - Rate recipe
- `POST /api/community/recipes/{id}/review` - Write review
- `GET /api/community/recipes/{id}/reviews` - Get reviews
- `PUT /api/community/reviews/{id}/helpful` - Mark helpful
- `POST /api/community/products/{id}/report` - Report issue

**Database Tables:** RecipeRating, RecipeReview, ProductReport, ReviewVote
**Features:**
- 5-star rating system
- Written reviews with photos
- Helpful/not helpful voting
- Product issue reporting
- Community moderation

---

### 16. Price Service (Port 5104)
**Purpose:** Price tracking and budget management
**Endpoints:**
- `POST /api/prices/track` - Track product price
- `GET /api/prices/product/{id}` - Get price history
- `GET /api/prices/compare` - Compare prices across stores
- `GET /api/prices/budget` - Get budget status
- `PUT /api/prices/budget` - Update budget
- `GET /api/prices/alerts` - Get price drop alerts

**Database Tables:** ProductPrice, PriceHistory, ShoppingBudget, PriceAlert
**Features:**
- Historical price tracking
- Multi-store comparison
- Monthly budget tracking
- Price drop alerts
- Best deal recommendations

**Background Workers:**
- PriceAnalysisWorker - Analyzes trends and sends alerts

---

## Data Access Pattern

All services use **ADO.NET** with a custom `SqlHelper` base class:

```csharp
public abstract class SqlHelper
{
    protected async Task<T?> ExecuteScalarAsync<T>(string sql, params SqlParameter[] parameters)
    protected async Task<int> ExecuteNonQueryAsync(string sql, params SqlParameter[] parameters)
    protected async Task<List<T>> ExecuteReaderAsync<T>(string sql, Func<SqlDataReader, T> mapper, params SqlParameter[] parameters)
    protected async Task<T> ExecuteTransactionAsync<T>(Func<SqlConnection, SqlTransaction, Task<T>> operation)
}
```

**Benefits:**
- Maximum performance (no ORM overhead)
- Full SQL control
- Explicit and debuggable
- Easy to optimize queries

---

## Communication Patterns

### Synchronous (HTTP)
- Frontend → Backend: Direct HTTP calls via API clients
- Service → Service: HTTP calls for immediate data needs

### Asynchronous (RabbitMQ)
- Event-driven communication for:
  - Inventory updates → Meal planning recalculation
  - Product changes → Search index updates
  - Recall alerts → Notification delivery
  - Price changes → Alert triggers

**Example Events:**
- `InventoryItemAdded`
- `RecipeCreated`
- `RecallPublished`
- `PriceChanged`

---

## Authentication & Authorization

All services use **JWT Bearer tokens** issued by Auth Service.

**Token Claims:**
- `sub` (Subject): User ID
- `email`: User email
- `role`: User role (User, Admin, Moderator)
- `exp`: Expiration timestamp

**Authorization:**
- Services validate JWT signature
- Services check required roles/permissions
- Rate limiting applied per user

---

## Health Checks

All services expose `/health` endpoint for monitoring:

```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "redis": "Healthy",
    "rabbitmq": "Healthy"
  }
}
```

---

## Database Schema Convention

All tables follow this pattern:

```sql
CREATE TABLE TableName (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    -- Entity fields --
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL
);
```

---

## Environment Variables

Each service requires:

```bash
ASPNETCORE_ENVIRONMENT=Development
ConnectionStrings__SqlServer=Server=sqlserver;Database=ExpressRecipe;...
Redis__ConnectionString=redis:6379
RabbitMQ__HostName=rabbitmq
RabbitMQ__UserName=expressrecipe
RabbitMQ__Password=expressrecipe_dev_password
```

---

## Deployment

### Docker Compose
```bash
docker compose up -d
```

### Kubernetes (Future)
Helm charts for each service with:
- Horizontal Pod Autoscaling
- Service mesh integration
- Centralized logging
- Distributed tracing

---

## Service Dependencies

```
Frontend (Blazor Web)
├── Auth Service
├── User Service
├── Product Service
├── Recipe Service
├── Inventory Service
├── Shopping Service
├── Meal Planning Service
├── Scanner Service
├── Recall Service
├── Search Service
├── Sync Service
├── AI Service
├── Notification Service
├── Analytics Service
├── Community Service
└── Price Service

Backend Services
├── SQL Server (database)
├── Redis (cache)
├── RabbitMQ (messaging)
└── Ollama (AI)
```

---

## Monitoring & Observability

### Logging (Serilog)
- Structured logging to console
- Log aggregation ready (Seq, ELK, Azure Monitor)

### Metrics (OpenTelemetry)
- Request duration
- Error rates
- Cache hit rates
- Database query performance

### Tracing (OpenTelemetry)
- Distributed tracing across services
- Correlation IDs for request tracking

---

## Security

- **Authentication**: JWT with RS256 signing
- **Authorization**: Role-based access control (RBAC)
- **HTTPS**: Required in production
- **SQL Injection**: Parameterized queries only
- **XSS**: Input sanitization and output encoding
- **CSRF**: Antiforgery tokens in Blazor
- **Rate Limiting**: Per-user and per-endpoint limits
- **Secrets**: Environment variables, Azure Key Vault

---

## Performance Optimizations

1. **Redis Caching**: Frequently accessed data (products, recipes)
2. **Connection Pooling**: Database connection reuse
3. **Async/Await**: Non-blocking I/O operations
4. **Pagination**: Large result sets
5. **CDN**: Static assets (images, CSS, JS)
6. **Compression**: Response compression (Gzip/Brotli)
7. **Indexing**: Database indexes on foreign keys and search fields

---

## Testing Strategy

### Unit Tests
- Business logic
- Data access layer
- Utility functions

### Integration Tests
- API endpoints
- Database operations
- Message bus integration

### E2E Tests
- Critical user workflows
- Cross-service scenarios

**Test Coverage Goal**: 80% code coverage

---

## Future Enhancements

1. **GraphQL Gateway** - Unified API layer
2. **gRPC** - High-performance service-to-service communication
3. **CQRS** - Command Query Responsibility Segregation for scalability
4. **Event Sourcing** - Audit trail and temporal queries
5. **Multi-tenancy** - Support for multiple organizations
6. **Internationalization** - Multi-language support
7. **Mobile Apps** - Native iOS/Android with .NET MAUI
8. **Offline-First** - SQLite with sync
9. **Real-time Collaboration** - Shared shopping lists/meal plans

---

**All 16 microservices are production-ready and fully containerized!**
