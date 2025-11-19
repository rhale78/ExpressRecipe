# ExpressRecipe - Microservices Breakdown

## Service Boundaries & Responsibilities

### Design Principles
- **Domain-Driven Design**: Services aligned with business domains
- **Single Responsibility**: Each service has one clear purpose
- **Loose Coupling**: Services communicate via well-defined contracts
- **High Cohesion**: Related functionality grouped together
- **Independent Deployment**: Services can be deployed independently

## Core Services

### 1. Auth Service
**Port**: 5100
**Database**: ExpressRecipe.Auth

**Responsibilities:**
- User authentication and authorization
- OAuth 2.0 / OpenID Connect implementation
- JWT token generation and validation
- Refresh token management
- External identity provider integration
- User session management
- Password reset and recovery

**Endpoints:**
- `POST /auth/register` - User registration
- `POST /auth/login` - User login
- `POST /auth/refresh` - Refresh access token
- `POST /auth/logout` - User logout
- `GET /auth/user` - Get current user
- `POST /auth/password/reset` - Password reset request
- `POST /auth/password/change` - Change password
- `GET /auth/.well-known/openid-configuration` - OIDC discovery

**Data Owned:**
- Users (ID, email, password hash, security stamps)
- External logins (provider, key)
- Refresh tokens
- Login attempts and security logs

**Dependencies:**
- Redis (session storage)
- Email service (password reset)

**Events Published:**
- `UserRegistered`
- `UserLoggedIn`
- `UserLoggedOut`
- `PasswordChanged`

---

### 2. User Profile Service
**Port**: 5101
**Database**: ExpressRecipe.Users

**Responsibilities:**
- User profile management
- Dietary restrictions and preferences
- Family member profiles
- Allergen severity tracking
- Medical condition management
- Preference learning from interactions

**Endpoints:**
- `GET /users/{id}` - Get user profile
- `PUT /users/{id}` - Update profile
- `POST /users/{id}/restrictions` - Add dietary restriction
- `DELETE /users/{id}/restrictions/{restrictionId}` - Remove restriction
- `GET /users/{id}/restrictions` - List all restrictions
- `POST /users/{id}/family` - Add family member
- `GET /users/{id}/preferences` - Get learned preferences
- `POST /users/{id}/preferences/feedback` - Record like/dislike

**Data Owned:**
- User profiles (name, age, preferences)
- Dietary restrictions (type, severity, notes)
- Allergens (substance, reaction type, severity)
- Medical conditions
- Family member profiles
- User preferences and learned behaviors
- Product feedback (liked/disliked)

**Dependencies:**
- Auth Service (user validation)

**Events Published:**
- `UserProfileUpdated`
- `RestrictionAdded`
- `RestrictionRemoved`
- `PreferenceLearned`

**Events Consumed:**
- `UserRegistered` (create default profile)

---

### 3. Product Service
**Port**: 5102
**Database**: ExpressRecipe.Products

**Responsibilities:**
- Product catalog management
- Ingredient database
- Product-ingredient relationships
- Barcode management
- Product search and filtering
- User-submitted products
- Manufacturer information

**Endpoints:**
- `GET /products/{id}` - Get product details
- `GET /products/barcode/{code}` - Find by barcode
- `GET /products/search` - Search products
- `POST /products` - Submit new product
- `PUT /products/{id}` - Update product
- `GET /products/{id}/ingredients` - Get product ingredients
- `POST /products/{id}/ingredients` - Add ingredient to product
- `GET /ingredients` - List ingredients
- `GET /ingredients/{id}` - Get ingredient details
- `POST /ingredients` - Add new ingredient
- `GET /products/{id}/compatibility/{userId}` - Check user compatibility

**Data Owned:**
- Products (name, brand, category, description, UPC)
- Ingredients (name, aliases, category)
- Product-Ingredient relationships (quantity, unit)
- Manufacturers (name, contact, website)
- Product images
- Nutritional information
- User submissions (pending approval)

**Dependencies:**
- User Service (compatibility checking)
- Image storage (Azure Blob/CDN)

**Events Published:**
- `ProductAdded`
- `ProductUpdated`
- `ProductIngredientChanged`
- `NewIngredientDiscovered`

**Events Consumed:**
- `UserPreferenceLearned` (update product recommendations)

---

### 4. Recipe Service
**Port**: 5103
**Database**: ExpressRecipe.Recipes

**Responsibilities:**
- Recipe management
- Recipe search and discovery
- User-submitted recipes
- Recipe ratings and reviews
- Ingredient substitution suggestions
- Nutritional calculation
- Recipe compatibility scoring

**Endpoints:**
- `GET /recipes` - List/search recipes
- `GET /recipes/{id}` - Get recipe details
- `POST /recipes` - Create recipe
- `PUT /recipes/{id}` - Update recipe
- `DELETE /recipes/{id}` - Delete recipe
- `POST /recipes/{id}/rate` - Rate recipe
- `GET /recipes/{id}/compatibility/{userId}` - Check user compatibility
- `GET /recipes/{id}/substitutions` - Get ingredient substitutions
- `POST /recipes/{id}/save` - Save to user collection
- `GET /recipes/user/{userId}` - User's saved recipes

**Data Owned:**
- Recipes (title, description, instructions, servings)
- Recipe ingredients (quantity, unit)
- Recipe steps
- Recipe images
- Recipe tags and categories
- User ratings and reviews
- Saved recipes per user
- Ingredient substitution rules

**Dependencies:**
- Product Service (ingredient data)
- User Service (compatibility checking)
- Image storage

**Events Published:**
- `RecipeCreated`
- `RecipeShared`
- `RecipeRated`
- `RecipeSaved`

**Events Consumed:**
- `IngredientAdded` (update recipe compatibility)
- `UserRestrictionChanged` (recalculate saved recipe compatibility)

---

### 5. Inventory Service
**Port**: 5104
**Database**: ExpressRecipe.Inventory

**Responsibilities:**
- Food inventory tracking (pantry, fridge, freezer)
- Expiration date monitoring
- Usage tracking and prediction
- Reorder suggestions
- Inventory alerts (expiring soon, low stock)
- Batch entry from receipts

**Endpoints:**
- `GET /inventory/user/{userId}` - Get user inventory
- `GET /inventory/user/{userId}/locations/{location}` - Get by location
- `POST /inventory/items` - Add inventory item
- `PUT /inventory/items/{id}` - Update item
- `DELETE /inventory/items/{id}` - Remove item
- `POST /inventory/items/{id}/use` - Record usage
- `GET /inventory/items/expiring` - Get expiring items
- `GET /inventory/predictions` - Get reorder predictions
- `POST /inventory/batch` - Batch add from receipt

**Data Owned:**
- Inventory items (product, quantity, location, purchase date, expiration)
- Usage history
- Storage locations
- Quantity predictions
- Expiration alerts

**Dependencies:**
- Product Service (product data)
- User Service (user validation)
- Receipt scanning service

**Events Published:**
- `ItemAdded`
- `ItemUsed`
- `ItemExpired`
- `ItemExpiringSoon`
- `ReorderSuggested`

**Events Consumed:**
- `ShoppingCompleted` (add purchased items)
- `RecipeCooked` (deduct ingredients)

---

### 6. Shopping Service
**Port**: 5105
**Database**: ExpressRecipe.Shopping

**Responsibilities:**
- Shopping list management
- List generation from meal plans
- Store organization
- Item checking off
- Shopping history
- Share lists with family
- Store-specific lists

**Endpoints:**
- `GET /shopping/lists/user/{userId}` - Get user's lists
- `GET /shopping/lists/{id}` - Get list details
- `POST /shopping/lists` - Create list
- `PUT /shopping/lists/{id}` - Update list
- `DELETE /shopping/lists/{id}` - Delete list
- `POST /shopping/lists/{id}/items` - Add item
- `PUT /shopping/lists/{id}/items/{itemId}/check` - Check off item
- `POST /shopping/lists/{id}/share` - Share with user
- `POST /shopping/lists/from-meal-plan/{mealPlanId}` - Generate from meal plan
- `GET /shopping/lists/{id}/organize/{storeId}` - Organize by store layout

**Data Owned:**
- Shopping lists
- List items (product, quantity, checked, notes)
- List sharing permissions
- Shopping history
- Store layouts

**Dependencies:**
- Product Service (product data)
- Recipe Service (meal plan data)
- Store Service (store layouts)
- User Service (sharing permissions)

**Events Published:**
- `ListCreated`
- `ItemAdded`
- `ItemChecked`
- `ShoppingCompleted`
- `ListShared`

**Events Consumed:**
- `MealPlanCreated` (auto-generate list option)
- `InventoryItemLow` (add to list)

---

### 7. Meal Planning Service
**Port**: 5106
**Database**: ExpressRecipe.MealPlanning

**Responsibilities:**
- Meal plan creation and management
- Calendar view
- Recipe scheduling
- Nutritional goals tracking
- Smart suggestions based on inventory
- Batch cooking support

**Endpoints:**
- `GET /meal-plans/user/{userId}` - Get user's meal plans
- `GET /meal-plans/{id}` - Get plan details
- `POST /meal-plans` - Create meal plan
- `PUT /meal-plans/{id}` - Update plan
- `DELETE /meal-plans/{id}` - Delete plan
- `POST /meal-plans/{id}/meals` - Add meal
- `GET /meal-plans/suggestions` - Get meal suggestions
- `GET /meal-plans/{id}/nutrition` - Get nutritional summary

**Data Owned:**
- Meal plans
- Scheduled meals (date, meal type, recipe)
- Nutritional goals
- Plan templates

**Dependencies:**
- Recipe Service (recipe data)
- Inventory Service (use what you have)
- User Service (dietary restrictions)

**Events Published:**
- `MealPlanCreated`
- `MealScheduled`
- `MealCooked`

**Events Consumed:**
- `InventoryExpiring` (use in meal plan)
- `RecipeSaved` (suggest for meal plan)

---

### 8. Price Service
**Port**: 5107
**Database**: ExpressRecipe.Pricing

**Responsibilities:**
- Price tracking
- Store price comparison
- Price history and trends
- Deal alerts
- Crowdsourced pricing data
- Regional pricing
- Best time to buy predictions

**Endpoints:**
- `GET /prices/product/{productId}` - Get current prices
- `GET /prices/product/{productId}/history` - Price history
- `POST /prices/submit` - Submit price observation
- `GET /prices/store/{storeId}/product/{productId}` - Store-specific price
- `GET /prices/deals` - Current deals
- `GET /prices/predictions/{productId}` - Best time to buy
- `GET /prices/compare` - Compare prices across stores

**Data Owned:**
- Price observations (product, store, price, date, user)
- Price history
- Deals and coupons
- Store locations
- Price predictions

**Dependencies:**
- Product Service (product data)
- User Service (regional data)
- Store Service (store data)

**Events Published:**
- `PriceUpdated`
- `DealFound`
- `PriceDrop`

**Events Consumed:**
- `ProductAdded` (initialize price tracking)
- `ShoppingCompleted` (record prices from receipt)

---

### 9. Scanner Service
**Port**: 5108
**Database**: ExpressRecipe.Scans

**Responsibilities:**
- Barcode scanning
- Label OCR processing
- Instant allergen alerts
- Product recognition
- Receipt scanning
- Image processing coordination

**Endpoints:**
- `POST /scanner/barcode` - Process barcode
- `POST /scanner/label` - OCR label image
- `POST /scanner/receipt` - Process receipt
- `GET /scanner/history/user/{userId}` - Scan history
- `POST /scanner/allergen-check` - Quick allergen check

**Data Owned:**
- Scan history (user, product, date, location, result)
- OCR results
- Unrecognized products

**Dependencies:**
- Product Service (product lookup)
- User Service (allergen checking)
- Azure Cognitive Services (OCR, image recognition)
- Azure Computer Vision (barcode reading)

**Events Published:**
- `ProductScanned`
- `AllergenDetected`
- `UnknownProductScanned`

**Events Consumed:**
- `ProductAdded` (may resolve unknown scans)

---

### 10. Recall Service
**Port**: 5109
**Database**: ExpressRecipe.Recalls

**Responsibilities:**
- FDA/USDA recall monitoring
- User recall alerts
- Product recall history
- Batch/lot code tracking
- Recall impact assessment

**Endpoints:**
- `GET /recalls` - List active recalls
- `GET /recalls/{id}` - Recall details
- `POST /recalls/check-inventory/{userId}` - Check user inventory
- `GET /recalls/product/{productId}` - Product recall history
- `POST /recalls/subscribe/{productId}` - Subscribe to alerts

**Data Owned:**
- Recalls (product, reason, date, severity, source)
- Affected batch/lot codes
- User recall subscriptions
- Recall notifications sent

**Dependencies:**
- Product Service (product matching)
- Inventory Service (inventory checking)
- Notification Service (alerts)
- FDA API
- USDA API

**Events Published:**
- `RecallIssued`
- `RecallAffectsUser`
- `RecallResolved`

**Events Consumed:**
- `InventoryItemAdded` (check against recalls)
- `ProductScanned` (check for recalls)

---

### 11. Notification Service
**Port**: 5110
**Database**: ExpressRecipe.Notifications

**Responsibilities:**
- Multi-channel notifications (email, push, SMS)
- Notification preferences
- Template management
- Delivery tracking
- Notification history

**Endpoints:**
- `POST /notifications/send` - Send notification
- `GET /notifications/user/{userId}` - User notifications
- `PUT /notifications/{id}/read` - Mark as read
- `GET /notifications/preferences/{userId}` - Get preferences
- `PUT /notifications/preferences/{userId}` - Update preferences

**Data Owned:**
- Notification queue
- Delivery status
- User preferences (email, push, SMS settings)
- Templates
- Notification history

**Dependencies:**
- Email provider (SendGrid, Amazon SES)
- Push notification service (Firebase, APNs)
- SMS provider (Twilio)

**Events Published:**
- `NotificationSent`
- `NotificationFailed`

**Events Consumed:**
- `ItemExpiring` (send alert)
- `RecallAffectsUser` (send urgent alert)
- `DealFound` (send if enabled)
- `PriceDrop` (send if watching)

---

### 12. Community Service
**Port**: 5111
**Database**: ExpressRecipe.Community

**Responsibilities:**
- User-generated content moderation
- Reviews and ratings
- Community forums
- Product submissions approval
- Reporting and flagging
- User reputation system

**Endpoints:**
- `GET /community/reviews/product/{productId}` - Product reviews
- `POST /community/reviews` - Submit review
- `POST /community/reports` - Report content
- `GET /community/forums` - List forums
- `GET /community/posts/{forumId}` - Forum posts
- `POST /community/posts` - Create post
- `GET /community/submissions/pending` - Pending submissions
- `POST /community/submissions/{id}/approve` - Approve submission

**Data Owned:**
- Reviews
- Ratings
- Forum posts and comments
- Reports and flags
- User reputation scores
- Moderation logs

**Dependencies:**
- Product Service (review targets)
- Recipe Service (recipe reviews)
- User Service (user data)

**Events Published:**
- `ReviewPosted`
- `ContentReported`
- `SubmissionApproved`
- `UserBanned`

**Events Consumed:**
- `ProductAdded` (user submission)
- `RecipeCreated` (user submission)

---

## Support Services

### 13. Sync Service
**Port**: 5112
**Database**: ExpressRecipe.Sync

**Responsibilities:**
- Client-server synchronization
- Conflict resolution
- Sync queue management
- Delta calculation
- Last sync tracking

**Endpoints:**
- `POST /sync/push` - Push local changes
- `POST /sync/pull` - Pull server changes
- `GET /sync/status/{userId}` - Sync status
- `POST /sync/resolve-conflict` - Manual conflict resolution

**Data Owned:**
- Sync metadata (last sync, device ID)
- Sync queue
- Conflict records
- Change vectors

**Dependencies:**
- All data services (sync targets)

**Events Published:**
- `SyncCompleted`
- `SyncConflict`

---

### 14. Search Service
**Port**: 5113
**Database**: Elasticsearch/Azure Search

**Responsibilities:**
- Full-text search across products, recipes, ingredients
- Faceted search
- Auto-complete
- Search suggestions
- Relevance tuning
- Search analytics

**Endpoints:**
- `GET /search` - Universal search
- `GET /search/products` - Product search
- `GET /search/recipes` - Recipe search
- `GET /search/suggest` - Auto-complete
- `GET /search/popular` - Popular searches

**Data Owned:**
- Search indices
- Search analytics
- Popular searches

**Dependencies:**
- Product Service (indexing)
- Recipe Service (indexing)

**Events Consumed:**
- `ProductAdded` (index)
- `RecipeCreated` (index)
- `ProductUpdated` (reindex)

---

### 15. Analytics Service
**Port**: 5114
**Database**: ExpressRecipe.Analytics (Time-series DB)

**Responsibilities:**
- Usage analytics
- Dietary trend analysis
- Product popularity tracking
- User behavior insights
- Business intelligence
- Anonymized data aggregation

**Endpoints:**
- `POST /analytics/track` - Track event
- `GET /analytics/dashboard` - Admin dashboard
- `GET /analytics/trends` - Dietary trends
- `GET /analytics/popular-products` - Popular products
- `GET /analytics/user-insights/{userId}` - User insights

**Data Owned:**
- Event tracking
- Aggregated statistics
- Trends
- User behavior patterns

**Dependencies:**
- All services (event sources)

**Events Consumed:**
- All events (tracking)

---

## Service Communication Matrix

| Service | Depends On | Publishes Events To |
|---------|------------|---------------------|
| Auth | Redis | User, Notification |
| User Profile | Auth | Recipe, Product, Inventory |
| Product | User, Storage | Recipe, Scanner, Price, Recall |
| Recipe | Product, User, Storage | Meal Plan, Shopping |
| Inventory | Product, User, Receipt | Shopping, Meal Plan, Notification |
| Shopping | Product, Recipe, User, Store | Inventory, Price |
| Meal Planning | Recipe, Inventory, User | Shopping |
| Price | Product, Store, User | Notification |
| Scanner | Product, User, Cognitive Services | Product, Inventory |
| Recall | Product, Inventory, Notification | Notification |
| Notification | Email, Push, SMS providers | All |
| Community | Product, Recipe, User | Product, Recipe |
| Sync | All data services | All |
| Search | Product, Recipe | None |
| Analytics | All services | None |

## Inter-Service Communication Patterns

### Synchronous Calls (gRPC/REST)
- User → Product (compatibility check)
- Scanner → Product (barcode lookup)
- Recipe → Product (ingredient data)
- Shopping → Store (layout data)

### Asynchronous Events (Message Bus)
- Product updates → Recipe (recompute compatibility)
- Inventory low → Shopping (add to list)
- Recall issued → Inventory (check affected items)
- Item expiring → Notification (send alert)

## Service Deployment Groups

### Critical Path (Always On, Auto-scale)
- Auth Service
- Product Service
- Scanner Service
- API Gateway

### User-Facing (High Availability)
- User Profile Service
- Recipe Service
- Shopping Service
- Inventory Service

### Background Processing (Scale-to-Zero)
- Recall Service (periodic checking)
- Analytics Service
- Search Indexing

### Support Services (Medium Priority)
- Notification Service
- Community Service
- Price Service
- Sync Service

## Next Steps
See database schemas in `03-DATA-MODELS.md`
