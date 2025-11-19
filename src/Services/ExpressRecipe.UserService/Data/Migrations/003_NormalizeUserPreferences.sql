-- Create Cuisine master table
CREATE TABLE Cuisine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Region NVARCHAR(100) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT UQ_Cuisine_Name UNIQUE (Name)
);
GO

CREATE INDEX IX_Cuisine_Region ON Cuisine(Region) WHERE IsDeleted = 0;
GO

-- Create HealthGoal master table
CREATE TABLE HealthGoal (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Category NVARCHAR(50) NULL, -- Weight Loss, Muscle Gain, Heart Health, etc.
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT UQ_HealthGoal_Name UNIQUE (Name)
);
GO

CREATE INDEX IX_HealthGoal_Category ON HealthGoal(Category) WHERE IsDeleted = 0;
GO

-- User preferred cuisines (many-to-many)
CREATE TABLE UserPreferredCuisine (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    CuisineId UNIQUEIDENTIFIER NOT NULL,
    PreferenceLevel INT NULL CHECK (PreferenceLevel BETWEEN 1 AND 5), -- 1=Low, 5=High
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_UserPreferredCuisine_Cuisine FOREIGN KEY (CuisineId)
        REFERENCES Cuisine(Id),
    CONSTRAINT UQ_UserPreferredCuisine_User_Cuisine UNIQUE (UserId, CuisineId)
);
GO

CREATE INDEX IX_UserPreferredCuisine_UserId ON UserPreferredCuisine(UserId) WHERE IsDeleted = 0;
CREATE INDEX IX_UserPreferredCuisine_CuisineId ON UserPreferredCuisine(CuisineId) WHERE IsDeleted = 0;
GO

-- User health goals (many-to-many)
CREATE TABLE UserHealthGoal (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    HealthGoalId UNIQUEIDENTIFIER NOT NULL,
    Priority INT NULL CHECK (Priority BETWEEN 1 AND 5), -- 1=Low, 5=High
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_UserHealthGoal_HealthGoal FOREIGN KEY (HealthGoalId)
        REFERENCES HealthGoal(Id),
    CONSTRAINT UQ_UserHealthGoal_User_Goal UNIQUE (UserId, HealthGoalId)
);
GO

CREATE INDEX IX_UserHealthGoal_UserId ON UserHealthGoal(UserId) WHERE IsDeleted = 0;
CREATE INDEX IX_UserHealthGoal_HealthGoalId ON UserHealthGoal(HealthGoalId) WHERE IsDeleted = 0;
GO

-- User favorite ingredients (references Product Service Ingredient table via ID)
CREATE TABLE UserFavoriteIngredient (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    IngredientId UNIQUEIDENTIFIER NOT NULL, -- References ProductService.Ingredient.Id
    Rating INT NULL CHECK (Rating BETWEEN 1 AND 5),
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT UQ_UserFavoriteIngredient_User_Ingredient UNIQUE (UserId, IngredientId)
);
GO

CREATE INDEX IX_UserFavoriteIngredient_UserId ON UserFavoriteIngredient(UserId) WHERE IsDeleted = 0;
CREATE INDEX IX_UserFavoriteIngredient_IngredientId ON UserFavoriteIngredient(IngredientId) WHERE IsDeleted = 0;
GO

-- User disliked ingredients
CREATE TABLE UserDislikedIngredient (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    IngredientId UNIQUEIDENTIFIER NOT NULL, -- References ProductService.Ingredient.Id
    Reason NVARCHAR(200) NULL, -- Taste, Texture, Ethical, etc.
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT UQ_UserDislikedIngredient_User_Ingredient UNIQUE (UserId, IngredientId)
);
GO

CREATE INDEX IX_UserDislikedIngredient_UserId ON UserDislikedIngredient(UserId) WHERE IsDeleted = 0;
CREATE INDEX IX_UserDislikedIngredient_IngredientId ON UserDislikedIngredient(IngredientId) WHERE IsDeleted = 0;
GO

-- Remove string fields from UserProfile and add cooking skill as FK
ALTER TABLE UserProfile
DROP COLUMN PreferredCuisines, DislikedFoods, HealthGoals;
GO
