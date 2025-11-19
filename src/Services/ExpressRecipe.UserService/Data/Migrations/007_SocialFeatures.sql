-- Migration: 007_SocialFeatures
-- Description: Add friends, comments, and family-specific scores
-- Date: 2024-11-19

-- UserFriend: Friend connections between users
CREATE TABLE UserFriend (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL, -- User who initiated or accepted
    FriendUserId UNIQUEIDENTIFIER NOT NULL, -- The friend
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Accepted, Blocked
    RequestedBy UNIQUEIDENTIFIER NOT NULL, -- Who sent the friend request
    RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    AcceptedAt DATETIME2 NULL,
    BlockedAt DATETIME2 NULL,
    BlockedBy UNIQUEIDENTIFIER NULL,
    Notes NVARCHAR(500) NULL,
    CONSTRAINT CK_UserFriend_NotSelf CHECK (UserId != FriendUserId),
    CONSTRAINT UQ_UserFriend_User_Friend UNIQUE (UserId, FriendUserId)
);

CREATE INDEX IX_UserFriend_UserId ON UserFriend(UserId);
CREATE INDEX IX_UserFriend_FriendUserId ON UserFriend(FriendUserId);
CREATE INDEX IX_UserFriend_Status ON UserFriend(Status);
GO

-- FriendInvitation: Invite non-users to join platform
CREATE TABLE FriendInvitation (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    InviterId UNIQUEIDENTIFIER NOT NULL, -- User sending invitation
    InviteeEmail NVARCHAR(200) NOT NULL,
    InviteePhone NVARCHAR(20) NULL,
    InvitationCode NVARCHAR(50) NOT NULL UNIQUE,
    InvitationMessage NVARCHAR(MAX) NULL,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Sent', -- Sent, Accepted, Expired
    SentAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    AcceptedAt DATETIME2 NULL,
    AcceptedByUserId UNIQUEIDENTIFIER NULL, -- User created from this invitation
    ExpiresAt DATETIME2 NOT NULL,
    CONSTRAINT UQ_FriendInvitation_Inviter_Email UNIQUE (InviterId, InviteeEmail)
);

CREATE INDEX IX_FriendInvitation_InviterId ON FriendInvitation(InviterId);
CREATE INDEX IX_FriendInvitation_InvitationCode ON FriendInvitation(InvitationCode);
CREATE INDEX IX_FriendInvitation_ExpiresAt ON FriendInvitation(ExpiresAt);
GO

-- RecipeComment: Comments on recipes (in Recipe Service conceptually, but stored centrally)
CREATE TABLE RecipeComment (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RecipeId UNIQUEIDENTIFIER NOT NULL, -- References RecipeService.Recipe.Id
    UserId UNIQUEIDENTIFIER NOT NULL,
    ParentCommentId UNIQUEIDENTIFIER NULL, -- For threaded comments/replies
    CommentText NVARCHAR(MAX) NOT NULL,
    IsEdited BIT NOT NULL DEFAULT 0,
    EditedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_RecipeComment_Parent FOREIGN KEY (ParentCommentId)
        REFERENCES RecipeComment(Id) ON DELETE NO ACTION
);

CREATE INDEX IX_RecipeComment_RecipeId ON RecipeComment(RecipeId);
CREATE INDEX IX_RecipeComment_UserId ON RecipeComment(UserId);
CREATE INDEX IX_RecipeComment_ParentCommentId ON RecipeComment(ParentCommentId);
CREATE INDEX IX_RecipeComment_CreatedAt ON RecipeComment(CreatedAt);
GO

-- ProductComment: Comments on products
CREATE TABLE ProductComment (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    ProductId UNIQUEIDENTIFIER NOT NULL, -- References ProductService.Product.Id
    UserId UNIQUEIDENTIFIER NOT NULL,
    ParentCommentId UNIQUEIDENTIFIER NULL,
    CommentText NVARCHAR(MAX) NOT NULL,
    IsEdited BIT NOT NULL DEFAULT 0,
    EditedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ProductComment_Parent FOREIGN KEY (ParentCommentId)
        REFERENCES ProductComment(Id) ON DELETE NO ACTION
);

CREATE INDEX IX_ProductComment_ProductId ON ProductComment(ProductId);
CREATE INDEX IX_ProductComment_UserId ON ProductComment(UserId);
CREATE INDEX IX_ProductComment_ParentCommentId ON ProductComment(ParentCommentId);
GO

-- RestaurantComment: Comments on restaurants
CREATE TABLE RestaurantComment (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    RestaurantId UNIQUEIDENTIFIER NOT NULL, -- References ProductService.Restaurant.Id
    UserId UNIQUEIDENTIFIER NOT NULL,
    ParentCommentId UNIQUEIDENTIFIER NULL,
    CommentText NVARCHAR(MAX) NOT NULL,
    IsEdited BIT NOT NULL DEFAULT 0,
    EditedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_RestaurantComment_Parent FOREIGN KEY (ParentCommentId)
        REFERENCES RestaurantComment(Id) ON DELETE NO ACTION
);

CREATE INDEX IX_RestaurantComment_RestaurantId ON RestaurantComment(RestaurantId);
CREATE INDEX IX_RestaurantComment_UserId ON RestaurantComment(UserId);
CREATE INDEX IX_RestaurantComment_ParentCommentId ON RestaurantComment(ParentCommentId);
GO

-- IngredientComment: Comments on ingredients
CREATE TABLE IngredientComment (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    IngredientId UNIQUEIDENTIFIER NULL, -- References ProductService.Ingredient.Id
    BaseIngredientId UNIQUEIDENTIFIER NULL, -- References ProductService.BaseIngredient.Id
    UserId UNIQUEIDENTIFIER NOT NULL,
    ParentCommentId UNIQUEIDENTIFIER NULL,
    CommentText NVARCHAR(MAX) NOT NULL,
    IsEdited BIT NOT NULL DEFAULT 0,
    EditedAt DATETIME2 NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_IngredientComment_Parent FOREIGN KEY (ParentCommentId)
        REFERENCES IngredientComment(Id) ON DELETE NO ACTION
);

CREATE INDEX IX_IngredientComment_IngredientId ON IngredientComment(IngredientId);
CREATE INDEX IX_IngredientComment_BaseIngredientId ON IngredientComment(BaseIngredientId);
CREATE INDEX IX_IngredientComment_UserId ON IngredientComment(UserId);
GO

-- CommentLike: Track likes on comments
CREATE TABLE CommentLike (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CommentType NVARCHAR(50) NOT NULL, -- Recipe, Product, Restaurant, Ingredient
    CommentId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_CommentLike_CommentType_CommentId ON CommentLike(CommentType, CommentId);
CREATE INDEX IX_CommentLike_UserId ON CommentLike(UserId);
CREATE UNIQUE INDEX UQ_CommentLike_Type_Comment_User ON CommentLike(CommentType, CommentId, UserId);
GO

-- FamilyScore: Family-specific scores for recipes, products, restaurants, ingredients
-- Allows tracking what the whole family likes vs. individual preferences
CREATE TABLE FamilyScore (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL, -- Primary user/family head
    EntityType NVARCHAR(50) NOT NULL, -- Recipe, Product, Restaurant, MenuItem, Ingredient
    EntityId UNIQUEIDENTIFIER NOT NULL,
    FamilyAverageScore DECIMAL(3, 2) NULL, -- Calculated average of all family member scores
    Notes NVARCHAR(MAX) NULL, -- Family notes about this item
    IsFavorite BIT NOT NULL DEFAULT 0,
    IsBlacklisted BIT NOT NULL DEFAULT 0, -- Never suggest this
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL
);

CREATE INDEX IX_FamilyScore_UserId ON FamilyScore(UserId);
CREATE INDEX IX_FamilyScore_EntityType_EntityId ON FamilyScore(EntityType, EntityId);
CREATE UNIQUE INDEX UQ_FamilyScore_User_Entity ON FamilyScore(UserId, EntityType, EntityId);
GO

-- FamilyMemberScore: Individual family member scores
CREATE TABLE FamilyMemberScore (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FamilyScoreId UNIQUEIDENTIFIER NOT NULL,
    FamilyMemberId UNIQUEIDENTIFIER NOT NULL, -- References FamilyMember.Id
    IndividualScore INT NOT NULL CHECK (IndividualScore >= 1 AND IndividualScore <= 5),
    Notes NVARCHAR(500) NULL,
    LastUpdated DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_FamilyMemberScore_FamilyScore FOREIGN KEY (FamilyScoreId)
        REFERENCES FamilyScore(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_FamilyMemberScore_Family_Member UNIQUE (FamilyScoreId, FamilyMemberId)
);

CREATE INDEX IX_FamilyMemberScore_FamilyScoreId ON FamilyMemberScore(FamilyScoreId);
CREATE INDEX IX_FamilyMemberScore_FamilyMemberId ON FamilyMemberScore(FamilyMemberId);
GO

-- UserActivity: Track user activity for engagement metrics
CREATE TABLE UserActivity (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ActivityType NVARCHAR(100) NOT NULL, -- Login, RecipeViewed, RecipeCooked, ProductScanned, etc.
    EntityType NVARCHAR(50) NULL,
    EntityId UNIQUEIDENTIFIER NULL,
    Metadata NVARCHAR(MAX) NULL, -- JSON for additional context
    ActivityDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    DeviceType NVARCHAR(50) NULL, -- Web, iOS, Android, Desktop
    IPAddress NVARCHAR(50) NULL
);

CREATE INDEX IX_UserActivity_UserId ON UserActivity(UserId);
CREATE INDEX IX_UserActivity_ActivityDate ON UserActivity(ActivityDate);
CREATE INDEX IX_UserActivity_ActivityType ON UserActivity(ActivityType);
GO
