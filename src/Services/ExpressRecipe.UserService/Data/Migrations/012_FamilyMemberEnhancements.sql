-- ========================================
-- ExpressRecipe User Service
-- Migration: 012_FamilyMemberEnhancements
-- Created: 2026-01-10
-- Description: Add user accounts, roles, relationships, favorites, and ratings for family members
-- ========================================

-- Add UserId column to FamilyMember table to link to actual user accounts
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'UserId')
BEGIN
    ALTER TABLE FamilyMember
    ADD UserId UNIQUEIDENTIFIER NULL;
END
GO

-- Add UserRole column to track admin/member/guest status
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'UserRole')
BEGIN
    ALTER TABLE FamilyMember
    ADD UserRole NVARCHAR(50) NULL DEFAULT 'Member'; -- Admin, Member, Guest
END
GO

-- Add flag to indicate if this family member has a user account
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'HasUserAccount')
BEGIN
    ALTER TABLE FamilyMember
    ADD HasUserAccount BIT NOT NULL DEFAULT 0;
END
GO

-- Add LinkedUserId for guest users sharing from another family
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'LinkedUserId')
BEGIN
    ALTER TABLE FamilyMember
    ADD LinkedUserId UNIQUEIDENTIFIER NULL;
END
GO

-- Add IsGuest flag for temporary access
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'IsGuest')
BEGIN
    ALTER TABLE FamilyMember
    ADD IsGuest BIT NOT NULL DEFAULT 0;
END
GO

-- Add Email for sending invitations/welcome emails
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'Email')
BEGIN
    ALTER TABLE FamilyMember
    ADD Email NVARCHAR(256) NULL;
END
GO

-- Create index on UserId
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'IX_FamilyMember_UserId')
BEGIN
    CREATE INDEX IX_FamilyMember_UserId ON FamilyMember(UserId) WHERE UserId IS NOT NULL AND IsDeleted = 0;
END
GO

-- Create FamilyRelationship table for managing relationships between family members
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FamilyRelationship')
BEGIN
    CREATE TABLE FamilyRelationship (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        FamilyMemberId1 UNIQUEIDENTIFIER NOT NULL,
        FamilyMemberId2 UNIQUEIDENTIFIER NOT NULL,
        RelationshipType NVARCHAR(50) NOT NULL, -- Parent, Child, Spouse, Sibling, Grandparent, Grandchild, etc.
        Notes NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy UNIQUEIDENTIFIER NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeletedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        CONSTRAINT FK_FamilyRelationship_FamilyMember1 FOREIGN KEY (FamilyMemberId1)
            REFERENCES FamilyMember(Id),
        CONSTRAINT FK_FamilyRelationship_FamilyMember2 FOREIGN KEY (FamilyMemberId2)
            REFERENCES FamilyMember(Id),
        CONSTRAINT CK_FamilyRelationship_DifferentMembers CHECK (FamilyMemberId1 != FamilyMemberId2)
    );

    CREATE INDEX IX_FamilyRelationship_Member1 ON FamilyRelationship(FamilyMemberId1) WHERE IsDeleted = 0;
    CREATE INDEX IX_FamilyRelationship_Member2 ON FamilyRelationship(FamilyMemberId2) WHERE IsDeleted = 0;
END
GO

-- Create UserFavoriteRecipe table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserFavoriteRecipe')
BEGIN
    CREATE TABLE UserFavoriteRecipe (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        RecipeId UNIQUEIDENTIFIER NOT NULL,
        Notes NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy UNIQUEIDENTIFIER NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeletedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        CONSTRAINT UQ_UserFavoriteRecipe_User_Recipe UNIQUE (UserId, RecipeId)
    );

    CREATE INDEX IX_UserFavoriteRecipe_UserId ON UserFavoriteRecipe(UserId) WHERE IsDeleted = 0;
    CREATE INDEX IX_UserFavoriteRecipe_RecipeId ON UserFavoriteRecipe(RecipeId) WHERE IsDeleted = 0;
END
GO

-- Create UserFavoriteProduct table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserFavoriteProduct')
BEGIN
    CREATE TABLE UserFavoriteProduct (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        ProductId UNIQUEIDENTIFIER NOT NULL,
        Notes NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy UNIQUEIDENTIFIER NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeletedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        CONSTRAINT UQ_UserFavoriteProduct_User_Product UNIQUE (UserId, ProductId)
    );

    CREATE INDEX IX_UserFavoriteProduct_UserId ON UserFavoriteProduct(UserId) WHERE IsDeleted = 0;
    CREATE INDEX IX_UserFavoriteProduct_ProductId ON UserFavoriteProduct(ProductId) WHERE IsDeleted = 0;
END
GO

-- Create UserProductRating table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserProductRating')
BEGIN
    CREATE TABLE UserProductRating (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UserId UNIQUEIDENTIFIER NOT NULL,
        ProductId UNIQUEIDENTIFIER NOT NULL,
        Rating INT NOT NULL, -- 1-5 stars
        ReviewText NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy UNIQUEIDENTIFIER NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeletedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        CONSTRAINT UQ_UserProductRating_User_Product UNIQUE (UserId, ProductId),
        CONSTRAINT CK_UserProductRating_Rating CHECK (Rating BETWEEN 1 AND 5)
    );

    CREATE INDEX IX_UserProductRating_UserId ON UserProductRating(UserId) WHERE IsDeleted = 0;
    CREATE INDEX IX_UserProductRating_ProductId ON UserProductRating(ProductId) WHERE IsDeleted = 0;
    CREATE INDEX IX_UserProductRating_Rating ON UserProductRating(Rating) WHERE IsDeleted = 0;
END
GO

PRINT 'Migration 012_FamilyMemberEnhancements completed successfully';
GO
