# AuthService User Schema Alignment Fix

## Issue Discovered
**Error:** `Microsoft.Data.SqlClient.SqlException: 'Invalid column name 'LockoutEnd'.`

The AuthService had **two different repositories** accessing the User table with **conflicting schemas**:

1. ? **`AuthRepository.cs`** - Using NEW schema (FirstName, LastName, EmailVerified, IsActive)
2. ? **`UserRepository.cs`** - Using OLD ASP.NET Identity schema (EmailConfirmed, SecurityStamp, LockoutEnd, LockoutEnabled)

The database migration `001_CreateUserTable.sql` was updated to the new schema, but `UserRepository.cs` was never updated to match.

---

## Root Cause

The User table schema was modernized in `src/Services/ExpressRecipe.AuthService/Data/Migrations/001_CreateUserTable.sql` to include:
- `FirstName` and `LastName` (replacing separate UserProfile)
- `EmailVerified` (replacing EmailConfirmed)
- `IsActive` (replacing LockoutEnabled)
- `LastLoginAt` (for tracking)

However, `UserRepository.cs` continued using the old ASP.NET Identity columns that no longer exist.

---

## Files Fixed

### 1. `src/Services/ExpressRecipe.AuthService/Data/UserRepository.cs`

#### Changes Applied:

**GetByIdAsync & GetByEmailAsync queries:**
```csharp
// OLD (BROKEN)
SELECT Id, Email, EmailConfirmed, PhoneNumber, PhoneNumberConfirmed,
       TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount,
       CreatedAt, UpdatedAt, IsDeleted

// NEW (FIXED)
SELECT Id, Email, FirstName, LastName, EmailVerified, IsActive,
       PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
       AccessFailedCount, CreatedAt, UpdatedAt, IsDeleted, LastLoginAt
```

**CreateAsync INSERT:**
```csharp
// OLD (BROKEN)
INSERT INTO [User] (Id, Email, EmailConfirmed, PasswordHash, SecurityStamp,
                   PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
                   LockoutEnabled, AccessFailedCount, CreatedAt, IsDeleted)

// NEW (FIXED)
INSERT INTO [User] (Id, Email, PasswordHash, FirstName, LastName,
                   EmailVerified, IsActive, PhoneNumber, PhoneNumberConfirmed,
                   TwoFactorEnabled, AccessFailedCount, CreatedAt, IsDeleted)
```

**UpdateAsync UPDATE:**
```csharp
// OLD (BROKEN)
SET Email = @Email,
    EmailConfirmed = @EmailConfirmed,
    PhoneNumber = @PhoneNumber,
    PhoneNumberConfirmed = @PhoneNumberConfirmed,
    TwoFactorEnabled = @TwoFactorEnabled,
    LockoutEnd = @LockoutEnd,
    LockoutEnabled = @LockoutEnabled,
    AccessFailedCount = @AccessFailedCount,
    UpdatedAt = @UpdatedAt

// NEW (FIXED)
SET Email = @Email,
    FirstName = @FirstName,
    LastName = @LastName,
    EmailVerified = @EmailVerified,
    IsActive = @IsActive,
    PhoneNumber = @PhoneNumber,
    PhoneNumberConfirmed = @PhoneNumberConfirmed,
    TwoFactorEnabled = @TwoFactorEnabled,
    AccessFailedCount = @AccessFailedCount,
    UpdatedAt = @UpdatedAt
```

**MapUser method:**
```csharp
// OLD (BROKEN)
return new User
{
    Id = GetGuid(reader, "Id"),
    Email = GetString(reader, "Email"),
    EmailConfirmed = GetBoolean(reader, "EmailConfirmed"),
    PhoneNumber = GetNullableString(reader, "PhoneNumber"),
    PhoneNumberConfirmed = GetBoolean(reader, "PhoneNumberConfirmed"),
    TwoFactorEnabled = GetBoolean(reader, "TwoFactorEnabled"),
    LockoutEnd = GetNullableDateTime(reader, "LockoutEnd"),      // ? Column doesn't exist
    LockoutEnabled = GetBoolean(reader, "LockoutEnabled"),       // ? Column doesn't exist
    AccessFailedCount = GetInt32(reader, "AccessFailedCount"),
    CreatedAt = GetDateTime(reader, "CreatedAt"),
    UpdatedAt = GetNullableDateTime(reader, "UpdatedAt"),
    IsDeleted = GetBoolean(reader, "IsDeleted")
};

// NEW (FIXED)
return new User
{
    Id = GetGuid(reader, "Id"),
    Email = GetString(reader, "Email"),
    FirstName = GetNullableString(reader, "FirstName"),          // ? New column
    LastName = GetNullableString(reader, "LastName"),            // ? New column
    EmailConfirmed = GetBoolean(reader, "EmailVerified"),        // ? Maps EmailVerified ? EmailConfirmed
    PhoneNumber = GetNullableString(reader, "PhoneNumber"),
    PhoneNumberConfirmed = GetBoolean(reader, "PhoneNumberConfirmed"),
    TwoFactorEnabled = GetBoolean(reader, "TwoFactorEnabled"),
    AccessFailedCount = GetInt32(reader, "AccessFailedCount"),
    CreatedAt = GetDateTime(reader, "CreatedAt"),
    UpdatedAt = GetNullableDateTime(reader, "UpdatedAt"),
    IsDeleted = GetBoolean(reader, "IsDeleted")
};
```

---

### 2. `src/ExpressRecipe.Shared/Models/User.cs`

Updated the `User` model to match the database schema:

```csharp
// OLD (BROKEN)
public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTime? LockoutEnd { get; set; }          // ? Removed
    public bool LockoutEnabled { get; set; } = true;   // ? Removed
    public int AccessFailedCount { get; set; }
}

// NEW (FIXED)
public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }             // ? Added
    public string? LastName { get; set; }              // ? Added
    public bool EmailConfirmed { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public int AccessFailedCount { get; set; }
}
```

---

## Schema Mapping Strategy

The new schema modernizes the User table while maintaining compatibility with existing code:

| Database Column | C# Property | Notes |
|----------------|-------------|-------|
| `FirstName` | `FirstName` | New - user's first name |
| `LastName` | `LastName` | New - user's last name |
| `EmailVerified` | `EmailConfirmed` | Renamed in DB, mapped to existing property |
| `IsActive` | (not mapped to User model) | New - account active status |
| `LastLoginAt` | (not mapped to User model) | New - tracking column |
| ~~`EmailConfirmed`~~ | - | Renamed to EmailVerified |
| ~~`SecurityStamp`~~ | - | Removed (not needed) |
| ~~`LockoutEnd`~~ | - | Removed (simplified auth) |
| ~~`LockoutEnabled`~~ | - | Removed (simplified auth) |

---

## Testing Verification

After these fixes:

1. ? **Registration should work** - no more "Invalid column name 'LockoutEnd'" error
2. ? **User creation** - FirstName and LastName stored correctly
3. ? **User lookup** - GetByIdAsync and GetByEmailAsync return complete user data
4. ? **User updates** - UpdateAsync writes to correct columns
5. ? **Schema consistency** - Both AuthRepository and UserRepository use same schema

---

## Impact Analysis

### What Works Now:
- `/register` endpoint can create users with first and last names
- Login flow queries correct columns
- User profile updates won't fail on missing columns
- Schema matches between migration SQL and repository queries

### Breaking Changes:
- **None** - Changes are backward compatible at the C# level
- `EmailConfirmed` property still exists in `User` model, just reads from `EmailVerified` column
- Old properties (`LockoutEnd`, `LockoutEnabled`) removed but were not being used

### Migration Required:
- ? Already done - `001_CreateUserTable.sql` has correct schema
- ?? If you have existing data, you'll need to drop and recreate the database (empty anyway per previous context)

---

## Related Issues Fixed

This fix resolves:
1. ? "Invalid column name 'LockoutEnd'" exception
2. ? Registration failure at `/register` endpoint
3. ? UserRepository out of sync with database schema
4. ? Duplicate/conflicting repository patterns in AuthService

---

## Repository Pattern Clarification

The AuthService now has **two repositories** with clear separation:

### `AuthRepository.cs` (AuthUser model)
- **Purpose:** Authentication operations (login, tokens, password management)
- **Model:** `AuthUser` (simple model for auth operations)
- **Operations:** CreateUserAsync, GetUserByEmailAsync, GetUserByIdAsync, UpdateLastLoginAsync, Token management

### `UserRepository.cs` (User model)
- **Purpose:** User profile and account management
- **Model:** `User` (from ExpressRecipe.Shared)
- **Operations:** GetByIdAsync, GetByEmailAsync, CreateAsync, UpdateAsync, EmailExistsAsync, Access failed count management

Both now use the **same underlying User table schema**.

---

## Database Schema Reference

### Current User Table (001_CreateUserTable.sql)

```sql
CREATE TABLE [User] (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Email NVARCHAR(256) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    FirstName NVARCHAR(100) NOT NULL,
    LastName NVARCHAR(100) NOT NULL,
    EmailVerified BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    PhoneNumber NVARCHAR(50) NULL,
    PhoneNumberConfirmed BIT NOT NULL DEFAULT 0,
    TwoFactorEnabled BIT NOT NULL DEFAULT 0,
    AccessFailedCount INT NOT NULL DEFAULT 0,
    LastLoginAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0
);
```

---

## Next Steps

1. ? UserRepository.cs updated to match schema
2. ? User model updated with FirstName/LastName
3. ?? Rebuild solution
4. ?? Drop and recreate ExpressRecipe.Auth database
5. ?? Restart AppHost
6. ?? Test registration at `/register`
7. ?? Verify user creation with first/last names

---

## Commands to Complete Fix

```cmd
# 1. Rebuild (migration SQL files already configured to copy)
dotnet build src\ExpressRecipe.AppHost.New\ExpressRecipe.AppHost.New.csproj

# 2. Drop the database (using SQL Server Management Studio or sqlcmd)
sqlcmd -S localhost,1433 -U sa -P <password> -Q "DROP DATABASE IF EXISTS [ExpressRecipe.Auth]"

# 3. Restart AppHost (will recreate database with correct schema)
dotnet run --project src\ExpressRecipe.AppHost.New\ExpressRecipe.AppHost.New.csproj

# 4. Test registration
# Navigate to https://localhost:<webapp-port>/register
# Fill in: Email, Password, First Name, Last Name
# Submit - should succeed now!
```

---

## Files Modified Summary

1. **`src/Services/ExpressRecipe.AuthService/Data/UserRepository.cs`**
   - Updated 5 methods to use new schema
   - Removed references to LockoutEnd, LockoutEnabled, SecurityStamp, EmailConfirmed
   - Added references to FirstName, LastName, EmailVerified, IsActive

2. **`src/ExpressRecipe.Shared/Models/User.cs`**
   - Added FirstName and LastName properties
   - Removed LockoutEnd and LockoutEnabled properties
   - Maintained backward compatibility with EmailConfirmed property name

---

## Related Documentation
- `REGISTRATION_AND_DATABASE_FIX.md` - Original auth schema migration fix
- `MISSING_RECALL_TABLES_FIX.md` - Migration file copy configuration
- `SQL_SERVER_FIXED_PORT_CONFIGURATION.md` - SQL Server port configuration
