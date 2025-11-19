-- Migration: 004_AddComments
-- Description: Add recipe comments and reactions system
-- Date: 2024-11-19

-- RecipeComment: User comments on recipes
CREATE TABLE RecipeComment (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    ParentCommentId UNIQUEIDENTIFIER NULL, -- For threaded replies
    CommentText NVARCHAR(MAX) NOT NULL,
    Rating INT NULL, -- Optional rating with comment (1-5 stars)
    LikesCount INT NOT NULL DEFAULT 0,
    DislikesCount INT NOT NULL DEFAULT 0,
    IsEdited BIT NOT NULL DEFAULT 0,
    IsFlagged BIT NOT NULL DEFAULT 0,
    FlagReason NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    DeletedBy UNIQUEIDENTIFIER NULL,
    CONSTRAINT FK_RecipeComment_Recipe FOREIGN KEY (RecipeId)
        REFERENCES Recipe(Id) ON DELETE CASCADE,
    CONSTRAINT FK_RecipeComment_Parent FOREIGN KEY (ParentCommentId)
        REFERENCES RecipeComment(Id) ON DELETE NO ACTION,
    CONSTRAINT CK_RecipeComment_Rating CHECK (Rating IS NULL OR (Rating >= 1 AND Rating <= 5))
);

CREATE INDEX IX_RecipeComment_RecipeId ON RecipeComment(RecipeId);
CREATE INDEX IX_RecipeComment_UserId ON RecipeComment(UserId);
CREATE INDEX IX_RecipeComment_ParentCommentId ON RecipeComment(ParentCommentId);
CREATE INDEX IX_RecipeComment_CreatedAt ON RecipeComment(CreatedAt);
CREATE INDEX IX_RecipeComment_IsFlagged ON RecipeComment(IsFlagged) WHERE IsFlagged = 1;
GO

-- CommentLike: Track user likes/dislikes on comments
CREATE TABLE CommentLike (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CommentId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    IsLike BIT NOT NULL, -- 1 = like, 0 = dislike
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_CommentLike_Comment FOREIGN KEY (CommentId)
        REFERENCES RecipeComment(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_CommentLike_Comment_User UNIQUE (CommentId, UserId)
);

CREATE INDEX IX_CommentLike_CommentId ON CommentLike(CommentId);
CREATE INDEX IX_CommentLike_UserId ON CommentLike(UserId);
GO
