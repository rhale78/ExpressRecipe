-- Migration: 013_FamilyMemberExtensions
-- Description: Add account-link columns to FamilyMember and create FamilyRelationship table
-- Date: 2026-03-03

-- Add account-linking columns to FamilyMember that are referenced by FamilyMemberRepository
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'UserId')
BEGIN
    ALTER TABLE FamilyMember ADD UserId UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'UserRole')
BEGIN
    ALTER TABLE FamilyMember ADD UserRole NVARCHAR(50) NOT NULL DEFAULT 'Member';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'HasUserAccount')
BEGIN
    ALTER TABLE FamilyMember ADD HasUserAccount BIT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'IsGuest')
BEGIN
    ALTER TABLE FamilyMember ADD IsGuest BIT NOT NULL DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'LinkedUserId')
BEGIN
    ALTER TABLE FamilyMember ADD LinkedUserId UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('FamilyMember') AND name = 'Email')
BEGIN
    ALTER TABLE FamilyMember ADD Email NVARCHAR(200) NULL;
END
GO

-- FamilyRelationship: Tracks directed relationships between family members
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID('FamilyRelationship') AND type = 'U')
BEGIN
    CREATE TABLE FamilyRelationship (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        FamilyMemberId1 UNIQUEIDENTIFIER NOT NULL,
        FamilyMemberId2 UNIQUEIDENTIFIER NOT NULL,
        RelationshipType NVARCHAR(100) NOT NULL,
        Notes NVARCHAR(MAX) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy UNIQUEIDENTIFIER NULL,
        UpdatedAt DATETIME2 NULL,
        UpdatedBy UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        DeletedAt DATETIME2 NULL,
        RowVersion ROWVERSION,
        CONSTRAINT FK_FamilyRelationship_Member1 FOREIGN KEY (FamilyMemberId1)
            REFERENCES FamilyMember(Id),
        CONSTRAINT FK_FamilyRelationship_Member2 FOREIGN KEY (FamilyMemberId2)
            REFERENCES FamilyMember(Id),
        CONSTRAINT CK_FamilyRelationship_NotSelf CHECK (FamilyMemberId1 != FamilyMemberId2)
    );

    CREATE INDEX IX_FamilyRelationship_Member1 ON FamilyRelationship(FamilyMemberId1) WHERE IsDeleted = 0;
    CREATE INDEX IX_FamilyRelationship_Member2 ON FamilyRelationship(FamilyMemberId2) WHERE IsDeleted = 0;
END
GO
