-- Migration: 005_EnhancedRatings
-- Description: Add family member tracking and per-member recipe ratings
-- Date: 2026-01-10

-- FamilyMember: Track household members for per-person ratings
CREATE TABLE FamilyMember (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL, -- Owner of the family/household
    Name NVARCHAR(200) NOT NULL,
    Nickname NVARCHAR(100) NULL,
    BirthDate DATE NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    DisplayOrder INT NOT NULL DEFAULT 0, -- Order to display members
    CreatedBy UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    CONSTRAINT UQ_FamilyMember_UserId_Name UNIQUE (UserId, Name)
);

CREATE INDEX IX_FamilyMember_UserId ON FamilyMember(UserId);
CREATE INDEX IX_FamilyMember_IsActive ON FamilyMember(IsActive);
GO

-- UserRecipeFamilyRating: Per-family-member ratings for recipes
-- Supports half-star ratings (using decimal rating)
CREATE TABLE UserRecipeFamilyRating (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL, -- Household owner
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    FamilyMemberId UNIQUEIDENTIFIER NULL, -- NULL means the user's own rating
    Rating DECIMAL(2, 1) NOT NULL CHECK (Rating >= 0 AND Rating <= 5 AND Rating % 0.5 = 0), -- 0, 0.5, 1.0, ..., 5.0
    Review NVARCHAR(MAX) NULL,
    WouldMakeAgain BIT NULL,
    MadeItDate DATE NULL, -- When they made/tried this recipe
    MadeItCount INT NOT NULL DEFAULT 0, -- How many times made
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CONSTRAINT FK_UserRecipeFamilyRating_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserRecipeFamilyRating_FamilyMember FOREIGN KEY (FamilyMemberId)
        REFERENCES FamilyMember(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_UserRecipeFamilyRating UNIQUE (UserId, RecipeId, FamilyMemberId)
);

CREATE INDEX IX_UserRecipeFamilyRating_UserId ON UserRecipeFamilyRating(UserId);
CREATE INDEX IX_UserRecipeFamilyRating_RecipeId ON UserRecipeFamilyRating(RecipeId);
CREATE INDEX IX_UserRecipeFamilyRating_FamilyMemberId ON UserRecipeFamilyRating(FamilyMemberId);
CREATE INDEX IX_UserRecipeFamilyRating_Rating ON UserRecipeFamilyRating(Rating);
GO

-- RecipeRating: Aggregated rating view for quick lookups
-- Includes overall average and per-member averages
CREATE TABLE RecipeRating (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    AverageRating DECIMAL(3, 2) NOT NULL, -- Overall average (0.00 - 5.00)
    TotalRatings INT NOT NULL DEFAULT 0,
    FiveStarCount INT NOT NULL DEFAULT 0,
    FourStarCount INT NOT NULL DEFAULT 0,
    ThreeStarCount INT NOT NULL DEFAULT 0,
    TwoStarCount INT NOT NULL DEFAULT 0,
    OneStarCount INT NOT NULL DEFAULT 0,
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_RecipeRating_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_RecipeRating_RecipeId UNIQUE (RecipeId)
);

CREATE INDEX IX_RecipeRating_RecipeId ON RecipeRating(RecipeId);
CREATE INDEX IX_RecipeRating_AverageRating ON RecipeRating(AverageRating);
GO

-- Stored procedure to update aggregated ratings
CREATE OR ALTER PROCEDURE UpdateRecipeRating
    @RecipeId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AverageRating DECIMAL(3, 2);
    DECLARE @TotalRatings INT;
    DECLARE @Five INT = 0;
    DECLARE @Four INT = 0;
    DECLARE @Three INT = 0;
    DECLARE @Two INT = 0;
    DECLARE @One INT = 0;

    -- Calculate statistics
    SELECT 
        @AverageRating = AVG(Rating),
        @TotalRatings = COUNT(*),
        @Five = SUM(CASE WHEN Rating >= 4.5 THEN 1 ELSE 0 END),
        @Four = SUM(CASE WHEN Rating >= 3.5 AND Rating < 4.5 THEN 1 ELSE 0 END),
        @Three = SUM(CASE WHEN Rating >= 2.5 AND Rating < 3.5 THEN 1 ELSE 0 END),
        @Two = SUM(CASE WHEN Rating >= 1.5 AND Rating < 2.5 THEN 1 ELSE 0 END),
        @One = SUM(CASE WHEN Rating >= 0.5 AND Rating < 1.5 THEN 1 ELSE 0 END)
    FROM UserRecipeFamilyRating
    WHERE RecipeId = @RecipeId;

    -- Upsert into RecipeRating table
    IF EXISTS (SELECT 1 FROM RecipeRating WHERE RecipeId = @RecipeId)
    BEGIN
        UPDATE RecipeRating
        SET AverageRating = ISNULL(@AverageRating, 0),
            TotalRatings = ISNULL(@TotalRatings, 0),
            FiveStarCount = @Five,
            FourStarCount = @Four,
            ThreeStarCount = @Three,
            TwoStarCount = @Two,
            OneStarCount = @One,
            UpdatedAt = GETUTCDATE()
        WHERE RecipeId = @RecipeId;
    END
    ELSE
    BEGIN
        INSERT INTO RecipeRating (RecipeId, AverageRating, TotalRatings, FiveStarCount, FourStarCount, ThreeStarCount, TwoStarCount, OneStarCount)
        VALUES (@RecipeId, ISNULL(@AverageRating, 0), ISNULL(@TotalRatings, 0), @Five, @Four, @Three, @Two, @One);
    END
END;
GO

-- Trigger to update aggregated ratings when family rating changes
CREATE OR ALTER TRIGGER TR_UserRecipeFamilyRating_AfterChange
ON UserRecipeFamilyRating
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    -- Update ratings for affected recipes
    DECLARE @RecipeId UNIQUEIDENTIFIER;

    -- Handle INSERTs and UPDATEs
    IF EXISTS (SELECT 1 FROM inserted)
    BEGIN
        DECLARE cur_inserted CURSOR FOR
        SELECT DISTINCT RecipeId FROM inserted;

        OPEN cur_inserted;
        FETCH NEXT FROM cur_inserted INTO @RecipeId;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            EXEC UpdateRecipeRating @RecipeId;
            FETCH NEXT FROM cur_inserted INTO @RecipeId;
        END;

        CLOSE cur_inserted;
        DEALLOCATE cur_inserted;
    END;

    -- Handle DELETEs
    IF EXISTS (SELECT 1 FROM deleted)
    BEGIN
        DECLARE cur_deleted CURSOR FOR
        SELECT DISTINCT RecipeId FROM deleted;

        OPEN cur_deleted;
        FETCH NEXT FROM cur_deleted INTO @RecipeId;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            EXEC UpdateRecipeRating @RecipeId;
            FETCH NEXT FROM cur_deleted INTO @RecipeId;
        END;

        CLOSE cur_deleted;
        DEALLOCATE cur_deleted;
    END;
END;
GO
