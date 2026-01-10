# User Profile Repository NULL Value Fix

## Issue

When creating a user profile during registration, the system crashed with:

```
System.Data.SqlTypes.SqlNullValueException: Data is Null. This method or property cannot be called on Null values.
```

This occurred in `UserProfileRepository.GetByUserIdAsync()` at line 33 when trying to read `DateTime` values from the database.

## Root Cause

The `GetByUserIdAsync` method was using non-nullable helper methods (`GetDateTime` and `GetDecimal`) to read fields that can be NULL in the database:

- `DateOfBirth` - can be NULL (user may not provide during registration)
- `SubscriptionExpiresAt` - can be NULL (free tier has no expiration)
- `HeightCm` - can be NULL (optional field)
- `WeightKg` - can be NULL (optional field)

When these fields were NULL in the database, calling the non-nullable helper methods threw `SqlNullValueException`.

## Solution

Updated `UserProfileRepository.GetByUserIdAsync()` to use nullable helper methods:

### Changed:
```csharp
DateOfBirth = GetDateTime(reader, "DateOfBirth"),
HeightCm = GetDecimal(reader, "HeightCm"),
WeightKg = GetDecimal(reader, "WeightKg"),
SubscriptionExpiresAt = GetDateTime(reader, "SubscriptionExpiresAt")
```

### To:
```csharp
DateOfBirth = GetNullableDateTime(reader, "DateOfBirth"),
HeightCm = GetNullableDecimal(reader, "HeightCm"),
WeightKg = GetNullableDecimal(reader, "WeightKg"),
SubscriptionExpiresAt = GetNullableDateTime(reader, "SubscriptionExpiresAt")
```

## Files Modified

- `src/Services/ExpressRecipe.UserService/Data/UserProfileRepository.cs`
  - Updated `GetByUserIdAsync` method to use nullable helper methods

## Verification

The DTOs already correctly defined these fields as nullable:
- `UserProfileDto.DateOfBirth` is `DateTime?`
- `UserProfileDto.SubscriptionExpiresAt` is `DateTime?`
- `UserProfileDto.HeightCm` is `decimal?`
- `UserProfileDto.WeightKg` is `decimal?`

The SqlHelper already provided the correct methods:
- `GetNullableDateTime()` - safely reads nullable DateTime
- `GetNullableDecimal()` - safely reads nullable decimal

## Testing

After this fix:
1. Register a new user (without providing optional fields)
2. AuthService creates user in auth database
3. AuthService calls UserService to create profile
4. UserService creates profile with NULL for optional fields
5. UserService retrieves profile successfully (no SqlNullValueException)
6. User dashboard loads without errors

## Impact

? **Profile creation now works** - no more crashes when reading newly created profiles

? **Handles NULL values properly** - optional fields can be NULL

? **Registration completes successfully** - users can register and login

? **Dashboard loads cleanly** - no 500 errors from profile retrieval

## Related

This fix completes the post-registration profile creation feature implemented in previous session:
- AuthService now creates profile after registration ?
- UserService creates profile with NULL optional fields ?
- UserService retrieves profile without crashing ? (this fix)
- Dashboard loads user data successfully ?
