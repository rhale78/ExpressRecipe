-- Migration: 002_CommunityRecipe
-- Description: Add CommunityRecipe table for the recipe gallery and approval pipeline
-- Date: 2026-03-10

CREATE TABLE CommunityRecipe (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId        UNIQUEIDENTIFIER NOT NULL,
    SubmittedBy     UNIQUEIDENTIFIER NOT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        -- Pending|InHumanQueue|AIReviewing|Approved|Rejected
    ApprovedAt      DATETIME2 NULL,
    ApprovedBy      NVARCHAR(100) NULL,      -- 'AI' or UserId.ToString()
    RejectionReason NVARCHAR(MAX) NULL,
    AIScore         DECIMAL(5,4) NULL,
    ViewCount       INT NOT NULL DEFAULT 0,
    FeaturedAt      DATETIME2 NULL,
    SubmittedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_CommunityRecipe_Status ON CommunityRecipe(Status);
CREATE INDEX IX_CommunityRecipe_SubmittedBy ON CommunityRecipe(SubmittedBy);
CREATE INDEX IX_CommunityRecipe_RecipeId ON CommunityRecipe(RecipeId);
GO

-- ApprovalQueue table for multi-entity approval pipeline
CREATE TABLE ApprovalQueue (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    EntityType      NVARCHAR(50) NOT NULL,   -- Product|Recipe
    EntityId        UNIQUEIDENTIFIER NOT NULL,
    ContentJson     NVARCHAR(MAX) NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    AIScore         DECIMAL(5,4) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ProcessedAt     DATETIME2 NULL
);

CREATE INDEX IX_ApprovalQueue_Status ON ApprovalQueue(Status);
CREATE INDEX IX_ApprovalQueue_EntityType ON ApprovalQueue(EntityType);
GO
