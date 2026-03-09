-- Migration: 010_VotingTables
-- Description: Add voting, post-meal review, and course review tables; add sharing flag to MealPlan

CREATE TABLE PlannedMealVote (
    Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PlannedMealId UNIQUEIDENTIFIER NOT NULL,
    VoterId       UNIQUEIDENTIFIER NOT NULL,
    Reaction      NVARCHAR(10) NOT NULL,
    Comment       NVARCHAR(300) NULL,
    VotedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_PlannedMealVote_PlannedMeal FOREIGN KEY (PlannedMealId) REFERENCES PlannedMeal(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_PlannedMealVote_Voter UNIQUE (PlannedMealId, VoterId)
);
CREATE INDEX IX_PlannedMealVote_PlannedMealId ON PlannedMealVote(PlannedMealId);
GO

CREATE TABLE PostMealReview (
    Id             UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PlannedMealId  UNIQUEIDENTIFIER NOT NULL,
    ReviewerId     UNIQUEIDENTIFIER NOT NULL,
    MealRating     TINYINT NOT NULL CHECK (MealRating BETWEEN 1 AND 5),
    Comment        NVARCHAR(500) NULL,
    WouldHaveAgain BIT NULL,
    ReviewedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_PostMealReview_PlannedMeal FOREIGN KEY (PlannedMealId) REFERENCES PlannedMeal(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_PostMealReview_Reviewer UNIQUE (PlannedMealId, ReviewerId)
);
CREATE INDEX IX_PostMealReview_PlannedMealId ON PostMealReview(PlannedMealId);
GO

CREATE TABLE PostMealCourseReview (
    Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PlannedMealId UNIQUEIDENTIFIER NOT NULL,
    RecipeId      UNIQUEIDENTIFIER NOT NULL,
    CourseType    NVARCHAR(50) NULL,
    ReviewerId    UNIQUEIDENTIFIER NOT NULL,
    Rating        TINYINT NOT NULL CHECK (Rating BETWEEN 1 AND 5),
    Comment       NVARCHAR(500) NULL,
    ReviewedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_PostMealCourseReview_PlannedMeal FOREIGN KEY (PlannedMealId) REFERENCES PlannedMeal(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_PostMealCourseReview_Reviewer UNIQUE (PlannedMealId, RecipeId, ReviewerId)
);
GO

ALTER TABLE MealPlan ADD IsSharedWithHousehold BIT NOT NULL DEFAULT 0;
GO
