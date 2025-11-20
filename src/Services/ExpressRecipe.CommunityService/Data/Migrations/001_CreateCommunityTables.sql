-- Migration: 001_CreateCommunityTables
-- Description: Create community contribution and moderation tables
-- Date: 2024-11-19

CREATE TABLE UserContribution (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ContributionType NVARCHAR(50) NOT NULL, -- Product, Recipe, Review, Report
    EntityType NVARCHAR(100) NOT NULL, -- Product, Recipe, etc.
    EntityId UNIQUEIDENTIFIER NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Approved, Rejected
    ReviewedBy UNIQUEIDENTIFIER NULL,
    ReviewedAt DATETIME2 NULL,
    ReviewNotes NVARCHAR(MAX) NULL,
    Points INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_UserContribution_UserId ON UserContribution(UserId);
CREATE INDEX IX_UserContribution_Status ON UserContribution(Status);
GO

CREATE TABLE ProductSubmission (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ProductName NVARCHAR(300) NOT NULL,
    Brand NVARCHAR(200) NULL,
    Barcode NVARCHAR(100) NULL,
    Category NVARCHAR(100) NULL,
    ImageUrl NVARCHAR(500) NULL,
    Ingredients NVARCHAR(MAX) NULL,
    NutritionData NVARCHAR(MAX) NULL, -- JSON
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    ApprovedProductId UNIQUEIDENTIFIER NULL,
    SubmittedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ReviewedAt DATETIME2 NULL,
    ReviewedBy UNIQUEIDENTIFIER NULL,
    RejectionReason NVARCHAR(MAX) NULL
);

CREATE INDEX IX_ProductSubmission_UserId ON ProductSubmission(UserId);
CREATE INDEX IX_ProductSubmission_Status ON ProductSubmission(Status);
CREATE INDEX IX_ProductSubmission_Barcode ON ProductSubmission(Barcode);
GO

CREATE TABLE UserReport (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ReportType NVARCHAR(50) NOT NULL, -- IncorrectInfo, Spam, Inappropriate, Other
    EntityType NVARCHAR(100) NOT NULL,
    EntityId UNIQUEIDENTIFIER NOT NULL,
    Reason NVARCHAR(MAX) NOT NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Open', -- Open, InReview, Resolved, Dismissed
    Priority NVARCHAR(50) NOT NULL DEFAULT 'Normal',
    AssignedTo UNIQUEIDENTIFIER NULL,
    Resolution NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ResolvedAt DATETIME2 NULL
);

CREATE INDEX IX_UserReport_Status ON UserReport(Status);
GO

CREATE TABLE CommunityReview (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    EntityType NVARCHAR(100) NOT NULL,
    EntityId UNIQUEIDENTIFIER NOT NULL,
    Rating INT NOT NULL CHECK (Rating >= 1 AND Rating <= 5),
    Title NVARCHAR(200) NULL,
    ReviewText NVARCHAR(MAX) NULL,
    IsVerifiedPurchase BIT NOT NULL DEFAULT 0,
    HelpfulCount INT NOT NULL DEFAULT 0,
    UnhelpfulCount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    
    CONSTRAINT UQ_CommunityReview_User_Entity UNIQUE (UserId, EntityType, EntityId)
);

CREATE INDEX IX_CommunityReview_EntityType_EntityId ON CommunityReview(EntityType, EntityId);
GO
