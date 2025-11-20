-- ========================================
-- ExpressRecipe Auth Service
-- Migration: 001_CreateUserTable
-- Created: 2025-11-19
-- Description: Create User table for authentication
-- ========================================

-- Create User table
CREATE TABLE [User] (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Email NVARCHAR(256) NOT NULL,
    EmailConfirmed BIT NOT NULL DEFAULT 0,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    SecurityStamp NVARCHAR(MAX) NOT NULL,
    PhoneNumber NVARCHAR(50) NULL,
    PhoneNumberConfirmed BIT NOT NULL DEFAULT 0,
    TwoFactorEnabled BIT NOT NULL DEFAULT 0,
    LockoutEnd DATETIME2 NULL,
    LockoutEnabled BIT NOT NULL DEFAULT 1,
    AccessFailedCount INT NOT NULL DEFAULT 0,

    -- Audit fields
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,

    CONSTRAINT UQ_User_Email UNIQUE (Email)
);

-- Create index on Email for fast lookups
CREATE INDEX IX_User_Email ON [User](Email) WHERE IsDeleted = 0;

-- Create index on LockoutEnd for checking locked accounts
CREATE INDEX IX_User_LockoutEnd ON [User](LockoutEnd) WHERE LockoutEnd IS NOT NULL;

-- ========================================

-- Create RefreshToken table
CREATE TABLE RefreshToken (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Token NVARCHAR(MAX) NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedByIp NVARCHAR(50) NULL,
    RevokedAt DATETIME2 NULL,
    RevokedByIp NVARCHAR(50) NULL,
    ReplacedByToken NVARCHAR(MAX) NULL,
    ReasonRevoked NVARCHAR(256) NULL,

    CONSTRAINT FK_RefreshToken_User FOREIGN KEY (UserId)
        REFERENCES [User](Id) ON DELETE CASCADE
);

-- Create index on UserId for fast lookups
CREATE INDEX IX_RefreshToken_UserId ON RefreshToken(UserId);

-- Create index on ExpiresAt for cleanup
CREATE INDEX IX_RefreshToken_ExpiresAt ON RefreshToken(ExpiresAt);

-- ========================================

-- Create ExternalLogin table (for OAuth providers)
CREATE TABLE ExternalLogin (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    LoginProvider NVARCHAR(128) NOT NULL,
    ProviderKey NVARCHAR(128) NOT NULL,
    ProviderDisplayName NVARCHAR(256) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_ExternalLogin_User FOREIGN KEY (UserId)
        REFERENCES [User](Id) ON DELETE CASCADE,
    CONSTRAINT UQ_ExternalLogin_Provider UNIQUE (LoginProvider, ProviderKey)
);

-- Create index on UserId
CREATE INDEX IX_ExternalLogin_UserId ON ExternalLogin(UserId);

-- ========================================

PRINT 'Migration 001_CreateUserTable completed successfully';
