-- Migration: 003_ReviewVoteDeduplication
-- Description: Add per-user vote tracking to prevent duplicate votes on reviews.
--              Also retroactively adds unique constraint on CommunityRecipe(RecipeId)
--              for databases that ran migration 002 before the constraint was added.
-- Date: 2026-03-11

-- Add unique constraint on CommunityRecipe.RecipeId if not already present
IF NOT EXISTS (
    SELECT 1 FROM sys.key_constraints
    WHERE name = 'UQ_CommunityRecipe_RecipeId' AND type = 'UQ'
)
BEGIN
    ALTER TABLE CommunityRecipe
    ADD CONSTRAINT UQ_CommunityRecipe_RecipeId UNIQUE (RecipeId);
END
GO

-- ReviewVote table: tracks which users have voted on each review (prevents duplicate votes)
CREATE TABLE ReviewVote (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ReviewId    UNIQUEIDENTIFIER NOT NULL,
    UserId      UNIQUEIDENTIFIER NOT NULL,
    IsHelpful   BIT NOT NULL,
    VotedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_ReviewVote_ReviewUser UNIQUE (ReviewId, UserId)
);

CREATE INDEX IX_ReviewVote_ReviewId ON ReviewVote(ReviewId);
GO
