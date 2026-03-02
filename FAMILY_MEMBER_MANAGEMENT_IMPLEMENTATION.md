# Family Member Management Feature - Implementation Complete

## Overview
This document summarizes the family member management feature implementation for ExpressRecipe, which allows users to manage family members with different roles, create user accounts for them, track relationships, manage favorites, and rate products.

## What Was Implemented

### 1. Database Schema (Migration 012_FamilyMemberEnhancements.sql)
**New Columns in FamilyMember Table:**
- `UserId` - Links family member to actual user account (nullable)
- `UserRole` - Role designation (Admin/Member/Guest)
- `HasUserAccount` - Boolean flag indicating if user account exists
- `IsGuest` - Boolean flag for temporary/guest access
- `LinkedUserId` - For guests sharing from another family
- `Email` - Email address for account creation

**New Tables:**
- `FamilyRelationship` - Many-to-many relationships between family members
  - Supports Parent, Child, Spouse, Sibling, Grandparent, Grandchild, etc.
- `UserFavoriteRecipe` - User's favorite recipes
- `UserFavoriteProduct` - User's favorite products  
- `UserProductRating` - Product ratings and reviews (1-5 stars)

### 2. Backend - UserService

**New Repositories:**
- `FamilyRelationshipRepository` - Manage family relationships
- `UserFavoritesRepository` - Manage recipe and product favorites
- `UserProductRatingRepository` - Manage product ratings

**Updated Repository:**
- `FamilyMemberRepository` - Extended with account creation and role management

**New Controllers:**
- `UserFavoritesController` - Endpoints for favorites and ratings
  - GET/POST/DELETE `/api/userfavorites/recipes/{recipeId}`
  - GET/POST/DELETE `/api/userfavorites/products/{productId}`
  - GET/POST/PUT/DELETE `/api/userfavorites/ratings/products/{productId}`
  - GET `/api/userfavorites/ratings/products/{productId}/stats` (public)

**Enhanced Controller:**
- `FamilyMembersController` - New endpoints:
  - POST `/api/familymembers/create-with-account` - Create member with user account
  - POST `/api/familymembers/{id}/dismiss` - Dismiss guest members
  - GET/POST/DELETE `/api/familymembers/{id}/relationships` - Manage relationships

### 3. Backend - AuthService
**New Endpoint:**
- POST `/api/auth/register-internal` - Internal service-to-service user creation
  - Used by UserService to create accounts for family members
  - No profile creation (handled by UserService)

### 4. Backend - NotificationService  
**New Endpoint:**
- POST `/api/notifications/send-email` - Send welcome emails to new family members
  - Accepts template name and data
  - Logs email requests (actual SMTP integration TBD)

### 5. Shared DTOs & Models

**Enhanced DTOs:**
- `FamilyMemberDto` - Added UserId, UserRole, HasUserAccount, IsGuest, Email, Relationships
- `CreateFamilyMemberRequest` - Added UserRole, IsGuest
- `CreateFamilyMemberWithAccountRequest` - Email, Password, SendWelcomeEmail
- `DismissGuestRequest` - Reason for dismissal

**New DTOs:**
- `FamilyRelationshipDto` - Relationship information
- `CreateFamilyRelationshipRequest` - Create relationship
- `UserFavoriteRecipeDto` - Recipe favorite
- `UserFavoriteProductDto` - Product favorite
- `UserProductRatingDto` - Product rating/review
- `CreateUserProductRatingRequest` - Create/update rating
- `ProductRatingStatsDto` - Aggregate rating stats

### 6. API Client (ExpressRecipe.Client.Shared)

**Updated Interface: IUserProfileApiClient**
- Family member management (create with account, dismiss guest)
- Relationship management
- Recipe favorites (add, remove, list)
- Product favorites (add, remove, list)
- Product ratings (create, update, delete, get stats)

### 7. Frontend - Blazor Web

**New Page:**
- `/profile/family` (FamilyManagement.razor)
  - View all family members with role badges
  - Add family member (with optional account creation)
  - Edit family member details
  - Delete family member
  - Dismiss guest members
  - Shows allergens and dietary restrictions
  - Modal-based add/edit interface

**Navigation:**
- Added "Family" link to main navigation menu

## User Roles Explained

### Admin
- Can manage all family members
- Can create/edit/delete any member
- Can dismiss guests
- Full access to family data

### Member
- Can manage their own profile only
- Cannot edit other family members
- Can view family information
- Standard user permissions

### Guest
- Temporary access
- View-only for most features
- Can be dismissed by admin
- Can share allergies/preferences with host family
- No permanent changes allowed

## Key Features

### 1. Account Creation
When adding a family member, users can optionally:
- Create a full user account with email and password
- Send automated welcome email
- Member gets their own login credentials
- Member can manage their own profile

### 2. Role-Based Access Control
- Family admins can edit any member
- Regular members can only edit themselves
- Guests have restricted access
- Clear visual indicators (badges) for roles

### 3. Guest Management
- Add temporary guests (friends, extended family)
- Guests can share allergen information
- Admin can dismiss guests when no longer needed
- Useful for holiday meals, gatherings, etc.

### 4. Relationship Tracking
- Define relationships between family members
- Parent, Child, Spouse, Sibling, Grandparent, etc.
- Bidirectional relationships
- Optional notes for context

### 5. Favorites & Ratings
- Each user can favorite recipes and products
- Rate products (1-5 stars) with optional reviews
- View aggregate rating statistics
- Helps with shopping and meal planning decisions

## API Endpoints Reference

### Family Members
```
GET    /api/familymembers                           - List family members
GET    /api/familymembers/{id}                      - Get family member
POST   /api/familymembers                           - Create family member
POST   /api/familymembers/create-with-account       - Create with user account
PUT    /api/familymembers/{id}                      - Update family member
DELETE /api/familymembers/{id}                      - Delete family member
POST   /api/familymembers/{id}/dismiss              - Dismiss guest
```

### Relationships
```
GET    /api/familymembers/{id}/relationships        - Get relationships
POST   /api/familymembers/{id}/relationships        - Create relationship
DELETE /api/familymembers/relationships/{id}        - Delete relationship
```

### Favorites
```
GET    /api/userfavorites/recipes                   - Get favorite recipes
POST   /api/userfavorites/recipes/{recipeId}        - Add favorite recipe
DELETE /api/userfavorites/recipes/{recipeId}        - Remove favorite recipe

GET    /api/userfavorites/products                  - Get favorite products
POST   /api/userfavorites/products/{productId}      - Add favorite product
DELETE /api/userfavorites/products/{productId}      - Remove favorite product
```

### Ratings
```
GET    /api/userfavorites/ratings                           - Get my ratings
GET    /api/userfavorites/ratings/products/{productId}      - Get my rating for product
POST   /api/userfavorites/ratings/products/{productId}      - Rate product
DELETE /api/userfavorites/ratings/products/{productId}      - Delete rating
GET    /api/userfavorites/ratings/products/{productId}/stats - Get aggregate stats (public)
```

## Usage Scenarios

### Scenario 1: Parent Adding Children
1. Parent (Admin) logs in
2. Goes to Family Management page
3. Adds each child without creating accounts initially
4. Assigns "Member" role to each
5. When children are old enough, creates accounts for them
6. Children can then log in and manage their own allergies/preferences

### Scenario 2: Hosting Thanksgiving
1. User adds friend as Guest member
2. Friend's allergen information is added
3. System alerts when scanning products that contain friend's allergens
4. After event, Admin dismisses guest

### Scenario 3: Spouse Co-Management
1. Primary user creates account for spouse
2. Assigns "Admin" role
3. Both can manage family members
4. Both can add/edit allergies, favorites, etc.
5. Changes sync across both accounts

## Future Enhancements

While the core functionality is complete, potential future additions:

1. **Allergen Management in UI** - Add allergen/dietary restriction editing in the family member modal
2. **Relationship Visualization** - Family tree view showing relationships
3. **Favorite Collections** - Group favorites into collections/lists
4. **Rating Analytics** - Charts and trends for product ratings
5. **Guest Invitations** - Email invitation system for guests
6. **Permission Granularity** - More fine-grained permissions beyond Admin/Member/Guest
7. **Profile Pictures** - Avatar support for family members
8. **Activity History** - Track who made what changes when

## Testing Checklist

- [ ] Create family member without account
- [ ] Create family member with account (verify welcome email logged)
- [ ] Edit family member as Admin
- [ ] Try to edit family member as non-Admin (should fail or be restricted)
- [ ] Delete family member
- [ ] Add guest member
- [ ] Dismiss guest member
- [ ] Create relationship between members
- [ ] Delete relationship
- [ ] Add recipe to favorites
- [ ] Remove recipe from favorites
- [ ] Add product to favorites
- [ ] Rate a product
- [ ] Update product rating
- [ ] View aggregate product ratings
- [ ] Test role-based UI visibility

## Technical Notes

### ADO.NET Usage
All data access uses ADO.NET directly (no Entity Framework) per project conventions:
- Custom `SqlHelper` base class
- Explicit SQL queries
- Full control over database operations
- Optimized performance

### Service-to-Service Communication
- UserService → AuthService (create user accounts)
- UserService → NotificationService (send emails)
- Uses HttpClient with configured base addresses

### Security Considerations
- JWT authentication required for all endpoints
- Role-based authorization enforced
- User can only access their own family data
- Guest dismissal restricted to admins

## Configuration Required

### AppSettings (UserService)
```json
{
  "Services": {
    "AuthService": "http://localhost:5001",
    "NotificationService": "http://localhost:5015"
  }
}
```

### Database Migration
Run migration 012_FamilyMemberEnhancements.sql on UserService database.

## Files Modified/Created

### Backend
- `src/Services/ExpressRecipe.UserService/Data/Migrations/012_FamilyMemberEnhancements.sql` ✨ NEW
- `src/Services/ExpressRecipe.UserService/Data/FamilyRelationshipRepository.cs` ✨ NEW
- `src/Services/ExpressRecipe.UserService/Data/UserFavoritesRepository.cs` ✨ NEW
- `src/Services/ExpressRecipe.UserService/Data/UserProductRatingRepository.cs` ✨ NEW
- `src/Services/ExpressRecipe.UserService/Data/FamilyMemberRepository.cs` 🔧 UPDATED
- `src/Services/ExpressRecipe.UserService/Controllers/FamilyMembersController.cs` 🔧 UPDATED
- `src/Services/ExpressRecipe.UserService/Controllers/UserFavoritesController.cs` ✨ NEW
- `src/Services/ExpressRecipe.UserService/Program.cs` 🔧 UPDATED
- `src/Services/ExpressRecipe.AuthService/Controllers/AuthController.cs` 🔧 UPDATED
- `src/Services/ExpressRecipe.NotificationService/Controllers/NotificationController.cs` 🔧 UPDATED

### Shared/DTOs
- `src/ExpressRecipe.Shared/DTOs/User/FamilyMemberDto.cs` 🔧 UPDATED
- `src/ExpressRecipe.Shared/DTOs/User/UserProfileDto.cs` 🔧 UPDATED

### API Client
- `src/ExpressRecipe.Client.Shared/Services/UserProfileApiClient.cs` 🔧 UPDATED
- `src/ExpressRecipe.Client.Shared/Models/User/UserProfileModels.cs` 🔧 UPDATED

### Frontend
- `src/Frontends/ExpressRecipe.BlazorWeb/Components/Pages/Profile/FamilyManagement.razor` ✨ NEW
- `src/Frontends/ExpressRecipe.BlazorWeb/Components/Layout/NavigationMenu.razor` 🔧 UPDATED

---

**Implementation Date:** January 10, 2026  
**Status:** Core Implementation Complete ✅  
**Tested:** Pending  
**Deployed:** Pending
