# Database Migration System

## Overview

The ExpressRecipe migration system provides a simple, code-based approach to managing database schema changes across all microservices. It tracks which migrations have been applied and ensures they run in the correct order.

## How It Works

### Migration Tracking

- Each database maintains a `__MigrationHistory` table
- This table records which migrations have been applied and when
- Migrations are identified by their filename (e.g., `001_CreateUserTable`)
- The system automatically skips migrations that have already been applied

### Migration Files

Migration files are SQL scripts stored in `Data/Migrations/` within each service project:

```
Services/
  ExpressRecipe.AuthService/
    Data/
      Migrations/
        001_CreateUserTable.sql
        002_AddExternalLogin.sql
        003_AddPasswordResetTokens.sql
```

### Naming Convention

Migration files must follow the naming pattern:
```
###_DescriptiveName.sql
```

Examples:
- `001_CreateUserTable.sql`
- `002_AddEmailVerification.sql`
- `010_AddIndexes.sql`

The numeric prefix determines execution order. Use increments of 1, 5, or 10 to allow for future insertions.

### SQL Batch Separators

SQL Server requires batch separators for certain DDL operations. Use `GO` on its own line:

```sql
CREATE TABLE Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY
);
GO

CREATE INDEX IX_Users_Email ON Users(Email);
GO
```

The migration runner will split the script by `GO` statements and execute each batch separately.

## Usage in Services

### 1. Add Migration Runner to Program.cs

```csharp
using ExpressRecipe.Data.Common;

var app = builder.Build();

// Run database migrations
var migrationsPath = Path.Combine(AppContext.BaseDirectory, "Data", "Migrations");
if (!Directory.Exists(migrationsPath))
{
    migrationsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Migrations");
}
var migrations = MigrationExtensions.LoadMigrationsFromDirectory(migrationsPath);
var connectionString = builder.Configuration.GetConnectionString("authdb")
    ?? throw new InvalidOperationException("Database connection string not found");
await app.RunMigrationsAsync(connectionString, migrations);
```

### 2. Include Migration Files in Build

Add to your `.csproj` file:

```xml
<ItemGroup>
  <None Include="Data\Migrations\*.sql" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### 3. Create Migration Files

Create SQL files in `Data/Migrations/`:

**001_CreateUserTable.sql:**
```sql
CREATE TABLE [User] (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Email NVARCHAR(256) NOT NULL,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE UNIQUE INDEX UQ_User_Email ON [User](Email) WHERE IsDeleted = 0;
GO
```

## Features

### Automatic Retry Logic

The migration runner includes retry logic for database connectivity:
- Waits for database to be available (useful with Docker containers)
- Retries up to 10 times with exponential backoff
- Logs connection attempts for debugging

### Transaction Support

Each migration runs in a transaction:
- If any part of the migration fails, all changes are rolled back
- Ensures database remains in consistent state
- Failed migrations must be fixed and service restarted

### Idempotent Execution

Migrations are idempotent:
- Safe to restart services multiple times
- Already-applied migrations are skipped
- Only new migrations execute

### Logging

Detailed logging at each step:
- Migration application started/completed
- Individual migration execution
- Database connection attempts
- Errors with full exception details

## Best Practices

### 1. Never Modify Existing Migrations

Once a migration is applied to any environment, never change it. Instead:
- Create a new migration to make additional changes
- This ensures all environments can replay the same history

### 2. Test Migrations Locally First

Before committing:
- Run the service locally
- Verify migration applies successfully
- Check database schema is correct
- Test that the application works with the new schema

### 3. Make Migrations Reversible When Possible

While we don't have automated rollback, design migrations so manual rollback is possible:
- Document the reverse operation in comments
- Avoid destructive changes when possible
- Use soft deletes instead of dropping tables

### 4. Keep Migrations Small and Focused

- One logical change per migration
- Easier to debug if something goes wrong
- Clearer history of schema evolution

### 5. Use Descriptive Names

Good:
- `001_CreateUserTable.sql`
- `002_AddEmailVerificationColumns.sql`
- `003_AddUserProfileTable.sql`

Bad:
- `001_Initial.sql`
- `002_Changes.sql`
- `003_Update.sql`

### 6. Handle Data Migration Carefully

When migrations involve data transformation:
- Test with production-like data volumes
- Consider performance implications
- Add appropriate indexes before bulk operations
- Consider doing data migrations offline for large datasets

## Troubleshooting

### Migration Fails on Startup

**Check logs for the specific error:**
```
Failed to apply migration 001_CreateUserTable: Invalid column name 'X'
```

**Common solutions:**
1. Fix the SQL in the migration file
2. If migration was partially applied, you may need to:
   - Delete the entry from `__MigrationHistory`
   - Manually undo any partial changes
   - Restart the service

### Migration Already Applied But Schema Doesn't Match

**This indicates the migration file was modified after being applied.**

**Solution:**
1. Create a new migration with the correct changes
2. Never modify existing migrations that have been applied

### Database Connection Timeouts

**If using Docker, ensure containers are starting in the correct order:**

In `AppHost/Program.cs`:
```csharp
var sqlServer = builder.AddSqlServer("sqlserver")
    .WithLifetime(ContainerLifetime.Persistent);

var authDb = sqlServer.AddDatabase("authdb", "ExpressRecipe.Auth");

var authService = builder.AddProject<Projects.ExpressRecipe_AuthService>("authservice")
    .WithReference(authDb);  // This ensures database starts first
```

### Manual Migration Management

**To see which migrations have been applied:**
```sql
SELECT * FROM __MigrationHistory ORDER BY AppliedAt;
```

**To manually mark a migration as applied (use with caution):**
```sql
INSERT INTO __MigrationHistory (MigrationId, AppliedAt)
VALUES ('001_CreateUserTable', GETUTCDATE());
```

**To undo a migration mark (does not reverse the schema changes):**
```sql
DELETE FROM __MigrationHistory WHERE MigrationId = '001_CreateUserTable';
```

## Examples

### Example: Adding a New Column

**005_AddUserPhoneNumber.sql:**
```sql
ALTER TABLE [User]
ADD PhoneNumber NVARCHAR(20) NULL;
GO

ALTER TABLE [User]
ADD PhoneNumberConfirmed BIT NOT NULL DEFAULT 0;
GO
```

### Example: Creating an Index

**006_AddUserEmailIndex.sql:**
```sql
CREATE NONCLUSTERED INDEX IX_User_Email
ON [User](Email)
WHERE IsDeleted = 0;
GO
```

### Example: Adding a Foreign Key

**007_AddUserProfileFK.sql:**
```sql
ALTER TABLE UserProfile
ADD CONSTRAINT FK_UserProfile_User
FOREIGN KEY (UserId) REFERENCES [User](Id)
ON DELETE CASCADE;
GO
```

### Example: Data Migration

**008_MigrateOldUserData.sql:**
```sql
-- Add new column
ALTER TABLE [User]
ADD FullName NVARCHAR(200) NULL;
GO

-- Migrate existing data
UPDATE [User]
SET FullName = CONCAT(FirstName, ' ', LastName)
WHERE FullName IS NULL;
GO

-- Make column required
ALTER TABLE [User]
ALTER COLUMN FullName NVARCHAR(200) NOT NULL;
GO

-- Drop old columns
ALTER TABLE [User]
DROP COLUMN FirstName, LastName;
GO
```

## Architecture Notes

### Why Not Entity Framework Migrations?

This project uses ADO.NET instead of Entity Framework, so we built a lightweight migration system that:
- Works with raw SQL for full control
- Has minimal dependencies
- Integrates with Aspire service startup
- Provides clear, auditable migration history
- Supports all SQL Server features without ORM limitations

### Future Enhancements

Potential improvements for future versions:
- Rollback support (down migrations)
- Migration validation (dry-run mode)
- Migration diffing tools
- Automated migration generation from schema changes
- Support for other database engines (PostgreSQL, MySQL)
