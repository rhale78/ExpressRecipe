# Post-Registration Fixes - Auto Profile Creation & API Route Fixes

## Summary
Fixed 404/405 errors on dashboard after user registration by:
1. Auto-creating user profile when user registers
2. Correcting API routes in ShoppingListApiClient
3. Adding stub implementations for missing endpoints

## Changes Made

### 1. AuthService - Auto Profile Creation

#### `src/Services/ExpressRecipe.AuthService/Controllers/AuthController.cs`
- Injected `IHttpClientFactory` to enable service-to-service communication
- Added profile creation call after successful user registration
- Profile creation is non-blocking - registration succeeds even if profile creation fails
- Logs warnings if profile creation fails but doesn't fail registration

```csharp
// After user creation, call UserService to create profile
var profileRequest = new CreateUserProfileForNewUserRequest
{
    UserId = userId,
    FirstName = request.FirstName ?? "",
    LastName = request.LastName ?? "",
    Email = request.Email
};

var userServiceClient = _httpClientFactory.CreateClient("UserService");
await userServiceClient.PostAsJsonAsync("/api/userprofile/system/create", profileRequest);
```

#### `src/Services/ExpressRecipe.AuthService/Program.cs`
- Added HttpClient configuration for UserService
- Uses `Services:UserService` config or fallback to `http://userservice`
- 30-second timeout for service-to-service calls

#### `src/Services/ExpressRecipe.AuthService/appsettings.Development.json`
- Added UserService URL: `https://localhost:61217`

### 2. UserService - Profile Creation Endpoint

#### `src/Services/ExpressRecipe.UserService/Controllers/UserProfileController.cs`
- Added `[HttpPost("system/create")]` endpoint with `[AllowAnonymous]` attribute
- Endpoint specifically for service-to-service profile creation during registration
- Checks if profile already exists and returns existing profile if found
- Creates basic profile with FirstName, LastName, Email
- Returns OK with profile data

#### `src/ExpressRecipe.Shared/DTOs/User/UserProfileDto.cs`
- Added `Email` property to `CreateUserProfileRequest`
- Added new `CreateUserProfileForNewUserRequest` DTO:
  - `UserId` (required)
  - `FirstName` (required, max 100 chars)
  - `LastName` (required, max 100 chars)
  - `Email` (required, valid email, max 256 chars)

### 3. Blazor Frontend - UserService Configuration

#### `src/Frontends/ExpressRecipe.BlazorWeb/appsettings.Development.json`
- Added UserService URL configuration: `https://localhost:61217`

### 4. ShoppingListApiClient - Route Fixes

#### `src/ExpressRecipe.Client.Shared/Services/ShoppingListApiClient.cs`

Fixed routes to match actual ShoppingService controller endpoints:

**Before ? After:**
- `/api/shopping/{id}` ? `/api/shopping/lists/{id}`
- `/api/shopping` ? `/api/shopping/lists`
- `/api/shopping/{id}/complete` ? `/api/shopping/lists/{id}/complete`
- `/api/shopping/{id}/archive` ? `/api/shopping/lists/{id}/archive`
- `/api/shopping/items` ? `/api/shopping/lists/{listId}/items`

**Stub implementations added (to avoid 404s):**
- `GetShoppingSummaryAsync()` - returns empty summary with zero counts
- `SearchShoppingListsAsync()` - returns empty result set

These stubs prevent 404 errors until the actual endpoints are implemented in ShoppingService.

## Testing Steps

1. **Register a new user**
   - Go to `/register`
   - Fill in: First Name, Last Name, Email, Password
   - Click Register

2. **Verify profile creation**
   - Check AuthService logs for: "User profile created successfully for user {UserId}"
   - Check UserService logs for: "Creating profile for new user {UserId} via system call"

3. **Verify dashboard loads without errors**
   - After registration, dashboard should redirect automatically
   - No 404 errors in browser console
   - No 405 Method Not Allowed errors

4. **Check user profile exists**
   - Navigate to `/profile` or user settings
   - Profile should show FirstName, LastName, Email from registration

## Benefits

? **Seamless registration experience** - Profile automatically created with user account

? **No 404/405 errors** - Dashboard loads cleanly after registration

? **Non-blocking profile creation** - Registration succeeds even if profile service is down

? **Proper service-to-service communication** - Uses AllowAnonymous endpoint pattern

? **Fixed API routes** - Shopping endpoints now match actual controller definitions

## Future Enhancements

- Implement actual `/api/shopping/summary` endpoint in ShoppingService
- Implement actual `/api/shopping/search` endpoint for list searching
- Add retry logic for profile creation failures
- Add profile creation to background queue for better resilience
- Implement compensating transaction if profile creation fails repeatedly

## Related Files

### AuthService
- `src/Services/ExpressRecipe.AuthService/Controllers/AuthController.cs`
- `src/Services/ExpressRecipe.AuthService/Program.cs`
- `src/Services/ExpressRecipe.AuthService/appsettings.Development.json`

### UserService
- `src/Services/ExpressRecipe.UserService/Controllers/UserProfileController.cs`

### Shared
- `src/ExpressRecipe.Shared/DTOs/User/UserProfileDto.cs`

### Blazor Frontend
- `src/Frontends/ExpressRecipe.BlazorWeb/appsettings.Development.json`
- `src/ExpressRecipe.Client.Shared/Services/ShoppingListApiClient.cs`

## Architecture Pattern

This implementation follows the **service-to-service communication pattern**:

1. AuthService creates user in its database
2. AuthService calls UserService via HTTP to create corresponding profile
3. UserService provides AllowAnonymous endpoint for system-level operations
4. Profile creation is non-blocking and logged for monitoring
5. User receives JWT token regardless of profile creation status

This ensures:
- Loose coupling between services
- Resilient registration flow
- Observable profile creation status
- No user-facing errors from profile creation failures
