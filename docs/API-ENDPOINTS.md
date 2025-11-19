# ExpressRecipe API Endpoints

Complete reference for all microservice endpoints in the ExpressRecipe platform.

**Last Updated:** 2025-11-19

---

## Authentication

All endpoints (except where noted) require JWT Bearer token authentication.

```http
Authorization: Bearer <token>
```

**User Identification:**
- User ID is extracted from JWT claims (`ClaimTypes.NameIdentifier`)
- Most endpoints operate on the authenticated user's data automatically

---

## 1. Auth Service

**Base URL:** `/api/auth` (port varies by environment)

### Public Endpoints

| Method | Endpoint | Description | Request Body | Response |
|--------|----------|-------------|--------------|----------|
| POST | `/register` | Register new user | `RegisterRequest` | `{ userId, email }` |
| POST | `/login` | Authenticate user | `LoginRequest` | `{ token, expiresAt, userId }` |
| POST | `/refresh` | Refresh access token | `RefreshTokenRequest` | `{ token, expiresAt }` |
| POST | `/forgot-password` | Request password reset | `{ email }` | `{ message }` |
| POST | `/reset-password` | Reset password with token | `ResetPasswordRequest` | `{ message }` |

---

## 2. User Service

**Base URL:** `/api` (various controllers)

### User Profiles

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/userprofiles/me` | Get current user's profile |
| GET | `/userprofiles/{id}` | Get user profile by ID |
| PUT | `/userprofiles/me` | Update current user's profile |
| DELETE | `/userprofiles/me` | Delete user account |

### Allergens

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/allergens` | Get all allergens |
| GET | `/allergens/user` | Get user's allergen list |
| POST | `/allergens/user` | Add allergen for user |
| DELETE | `/allergens/user/{id}` | Remove allergen from user |

### Enhanced Allergen Tracking

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/allergymanagement/summary` | Get allergen tracking summary |
| GET | `/allergymanagement/reaction-types` | Get all reaction types |
| GET | `/allergymanagement/ingredient-allergies` | Get user's ingredient allergies |
| POST | `/allergymanagement/ingredient-allergies` | Add ingredient allergy |
| PUT | `/allergymanagement/ingredient-allergies/{id}` | Update ingredient allergy |
| DELETE | `/allergymanagement/ingredient-allergies/{id}` | Delete ingredient allergy |
| GET | `/allergymanagement/incidents` | Get allergy incidents |
| POST | `/allergymanagement/incidents` | Log allergy incident |

### Dietary Restrictions

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/dietaryrestrictions` | Get all dietary restrictions |
| GET | `/dietaryrestrictions/user` | Get user's restrictions |
| POST | `/dietaryrestrictions/user` | Add restriction for user |
| DELETE | `/dietaryrestrictions/user/{id}` | Remove restriction |

### Family Members

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/familymembers` | Get user's family members |
| POST | `/familymembers` | Add family member |
| PUT | `/familymembers/{id}` | Update family member |
| DELETE | `/familymembers/{id}` | Delete family member |

### Family Scores

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/familyscores` | Get user's family scores |
| GET | `/familyscores/{entityType}/{entityId}` | Get score for specific entity |
| GET | `/familyscores/favorites` | Get favorites |
| GET | `/familyscores/blacklisted` | Get blacklisted items |
| POST | `/familyscores` | Create family score |
| PUT | `/familyscores/{id}` | Update family score |
| DELETE | `/familyscores/{id}` | Delete family score |
| POST | `/familyscores/{id}/members` | Add member score |
| PUT | `/familyscores/members/{id}` | Update member score |
| DELETE | `/familyscores/members/{id}` | Delete member score |

### User Preferences

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/userpreferences` | Get user preferences |
| PUT | `/userpreferences` | Update preferences |
| GET | `/userpreferences/cuisines` | Get cuisine preferences |
| POST | `/userpreferences/cuisines` | Add cuisine preference |
| DELETE | `/userpreferences/cuisines/{id}` | Remove cuisine |

### Points & Gamification

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/points/summary` | Get points summary |
| GET | `/points/balance` | Get current balance |
| GET | `/points/transactions` | Get transaction history |
| GET | `/points/contributions` | Get user contributions |
| GET | `/points/contribution-types` | Get all contribution types |
| GET | `/points/rewards` | Get available rewards |
| GET | `/points/redeemed-rewards` | Get user's redeemed rewards |
| POST | `/points/redeem` | Redeem reward |
| POST | `/points/contributions/{id}/approve` | Approve contribution (Admin) |

### Friends & Social

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/friends/summary` | Get friends summary |
| GET | `/friends` | Get friends list |
| POST | `/friends/request` | Send friend request |
| POST | `/friends/accept` | Accept friend request |
| POST | `/friends/reject` | Reject friend request |
| DELETE | `/friends/{friendUserId}` | Remove friend |
| POST | `/friends/block` | Block user |
| POST | `/friends/unblock/{id}` | Unblock user |
| POST | `/friends/invite` | Generate invitation code |
| GET | `/friends/invitations` | Get user's invitations |
| POST | `/friends/accept-invitation` | Accept invitation by code |

### Reports

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/reports/types` | Get report types |
| GET | `/reports/saved` | Get saved reports |
| POST | `/reports/saved` | Create saved report |
| PUT | `/reports/saved/{id}` | Update saved report |
| DELETE | `/reports/saved/{id}` | Delete saved report |
| GET | `/reports/history` | Get report history |
| POST | `/reports/history` | Generate report |

### Lists

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/lists` | Get user's lists |
| GET | `/lists/{id}` | Get list by ID |
| GET | `/lists/shared` | Get lists shared with user |
| POST | `/lists` | Create list |
| PUT | `/lists/{id}` | Update list |
| DELETE | `/lists/{id}` | Delete list |
| GET | `/lists/{id}/items` | Get list items |
| POST | `/lists/{id}/items` | Add item to list |
| PUT | `/lists/items/{itemId}` | Update list item |
| DELETE | `/lists/items/{itemId}` | Delete list item |
| PATCH | `/lists/items/{itemId}/check` | Check/uncheck item |
| POST | `/lists/{id}/share` | Share list |
| PUT | `/lists/sharing/{sharingId}` | Update sharing permissions |
| DELETE | `/lists/sharing/{sharingId}` | Remove sharing |

### Subscriptions

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/subscriptions/tiers` | Get subscription tiers (Public) |
| GET | `/subscriptions/current` | Get current subscription |
| POST | `/subscriptions/subscribe` | Subscribe to tier |
| POST | `/subscriptions/cancel` | Cancel subscription |
| PUT | `/subscriptions/{id}/payment-method` | Update payment method |
| GET | `/subscriptions/history` | Get subscription history |
| GET | `/subscriptions/features/{featureName}` | Check feature access |
| GET | `/subscriptions/features` | Get all feature access |

### Activity Tracking

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/activity` | Get activity history |
| GET | `/activity/recent` | Get recent activity (last N days) |
| GET | `/activity/type/{activityType}` | Get activity by type |
| GET | `/activity/summary` | Get activity summary |
| GET | `/activity/counts` | Get activity counts by type |
| GET | `/activity/streak/current` | Get current streak |
| GET | `/activity/streak/longest` | Get longest streak |
| GET | `/activity/today` | Check if user has activity today |
| POST | `/activity` | Log activity |

---

## 3. Product Service

**Base URL:** `/api` (various controllers)

### Products

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/products` | Search products |
| GET | `/products/{id}` | Get product by ID |
| GET | `/products/barcode/{upc}` | Get product by UPC |
| POST | `/products` | Create product |
| PUT | `/products/{id}` | Update product |
| DELETE | `/products/{id}` | Delete product |

### Ingredients

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/ingredients` | Search ingredients |
| GET | `/ingredients/{id}` | Get ingredient by ID |
| POST | `/ingredients` | Create ingredient |

### Stores

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/stores/search` | Search stores |
| GET | `/stores/nearby` | Get nearby stores (geo-location) |
| GET | `/stores/{id}` | Get store by ID |
| POST | `/stores` | Create store |
| PUT | `/stores/{id}` | Update store |
| DELETE | `/stores/{id}` | Delete store |

### Coupons

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/coupons/search` | Search coupons |
| GET | `/coupons/product/{productId}` | Get coupons for product |
| GET | `/coupons/my-coupons` | Get user's clipped coupons |
| POST | `/coupons` | Create coupon |
| PUT | `/coupons/{id}` | Update coupon |
| DELETE | `/coupons/{id}` | Delete coupon |
| POST | `/coupons/clip` | Clip coupon |
| POST | `/coupons/use` | Use coupon |

### Restaurants

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/restaurants/search` | Search restaurants |
| GET | `/restaurants/nearby` | Get nearby restaurants |
| GET | `/restaurants/{id}` | Get restaurant by ID |
| POST | `/restaurants` | Create restaurant |

### Menu Items

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/menuitems/restaurant/{restaurantId}` | Get menu items for restaurant |
| GET | `/menuitems/{id}` | Get menu item by ID |
| POST | `/menuitems` | Create menu item |

---

## 4. Recipe Service

**Base URL:** `/api` (various controllers)

### Recipes

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/recipes` | Search recipes |
| GET | `/recipes/{id}` | Get recipe by ID |
| GET | `/recipes/user` | Get user's recipes |
| POST | `/recipes` | Create recipe |
| PUT | `/recipes/{id}` | Update recipe |
| DELETE | `/recipes/{id}` | Delete recipe |

### Recipe Import

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/recipeimport/summary` | Get import summary |
| GET | `/recipeimport/sources` | Get import sources |
| GET | `/recipeimport/jobs` | Get user's import jobs |
| GET | `/recipeimport/jobs/{id}` | Get import job details |
| POST | `/recipeimport/jobs` | Create import job |
| GET | `/recipeimport/versions/{recipeId}` | Get recipe versions |
| GET | `/recipeimport/forks/{recipeId}` | Get recipe forks |
| GET | `/recipeimport/exports` | Get export history |

### Recipe Collections

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/recipecollections/summary` | Get collections summary |
| GET | `/recipecollections` | Get user's collections |
| GET | `/recipecollections/{id}` | Get collection by ID |
| POST | `/recipecollections` | Create collection |
| PUT | `/recipecollections/{id}` | Update collection |
| DELETE | `/recipecollections/{id}` | Delete collection |
| POST | `/recipecollections/{id}/recipes` | Add recipe to collection |
| DELETE | `/recipecollections/{collectionId}/recipes/{recipeId}` | Remove recipe |
| PUT | `/recipecollections/items/{itemId}` | Update collection item |

### Comments

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/comments/recipe/{recipeId}` | Get comments for recipe |
| GET | `/comments/{id}` | Get comment by ID |
| GET | `/comments/{id}/replies` | Get comment replies |
| GET | `/comments/user` | Get user's comments |
| GET | `/comments/flagged` | Get flagged comments (Admin) |
| POST | `/comments` | Create comment |
| PUT | `/comments/{id}` | Update comment |
| DELETE | `/comments/{id}` | Delete comment |
| POST | `/comments/{id}/like` | Like comment |
| DELETE | `/comments/{id}/like` | Unlike comment |
| POST | `/comments/{id}/dislike` | Dislike comment |
| DELETE | `/comments/{id}/dislike` | Undislike comment |
| POST | `/comments/{id}/flag` | Flag comment |
| DELETE | `/comments/{id}/flag` | Unflag comment (Admin) |

---

## Common Patterns

### Pagination

Many list endpoints support pagination:

```
GET /api/resource?pageNumber=1&pageSize=50
```

- `pageNumber`: 1-based page number (default: 1)
- `pageSize`: Items per page (default: 50, max: 100)

### Filtering

Endpoints support various filters via query parameters:

```
GET /api/products?category=Dairy&brand=Organic
GET /api/activity/recent?days=30
GET /api/familyscores?entityType=Recipe&favoritesOnly=true
```

### Search

Search endpoints typically support:

```
GET /api/products?query=milk&sortBy=name&ascending=true
```

### Geo-Location

Store/restaurant search supports location-based queries:

```
GET /api/stores/nearby?latitude=37.7749&longitude=-122.4194&radiusMiles=10
```

### Date Ranges

Report and history endpoints support date filtering:

```
GET /api/reports/history?startDate=2024-01-01&endDate=2024-12-31
GET /api/activity/summary?startDate=2024-11-01&endDate=2024-11-30
```

---

## Response Formats

### Success Response

```json
{
  "data": { ... },
  "message": "Success message (optional)"
}
```

### Error Response

```json
{
  "message": "Error description",
  "errors": {
    "field": ["Validation error"]
  }
}
```

### HTTP Status Codes

- `200 OK` - Success
- `201 Created` - Resource created
- `204 No Content` - Success with no response body
- `400 Bad Request` - Invalid request
- `401 Unauthorized` - Authentication required
- `403 Forbidden` - Insufficient permissions
- `404 Not Found` - Resource not found
- `500 Internal Server Error` - Server error

---

## Subscription Tiers & Features

| Feature | Free | Plus ($4.99/mo) | Premium ($9.99/mo) |
|---------|------|-----------------|---------------------|
| Max Recipes | 50 | Unlimited | Unlimited |
| Family Members | 1 | 4 | 8 |
| Recipe Import | ❌ | ✅ | ✅ |
| Offline Sync | ❌ | ✅ | ✅ |
| Menu Planning | ❌ | ✅ | ✅ |
| Price Tracking | ❌ | ✅ | ✅ |
| Advanced Reports | ❌ | ❌ | ✅ |
| Inventory Tracking | ❌ | ✅ | ✅ |
| Points Multiplier | 1.0x | 1.5x | 2.0x |

---

## Background Services

The platform includes several background services:

1. **SubscriptionRenewalService** - Processes subscription renewals automatically
2. **ScheduledReportsService** - Generates scheduled reports
3. **PointsManagementService** - Awards daily bonuses, manages streaks

## Middleware

1. **ActivityTrackingMiddleware** - Automatically logs user activities based on API calls

---

## Rate Limiting

(To be implemented)

- Anonymous: 100 requests/hour
- Authenticated: 1000 requests/hour
- Premium: 5000 requests/hour

---

## API Versioning

Current version: **v1**

All endpoints are prefixed with `/api/` and documented as v1 in Swagger.

---

*For detailed request/response schemas, see the Swagger documentation at `/swagger` when running in Development mode.*
