-- User Profile Table
CREATE TABLE UserProfile (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL UNIQUE,
    FirstName NVARCHAR(100) NULL,
    LastName NVARCHAR(100) NULL,
    DateOfBirth DATE NULL,
    Gender NVARCHAR(20) NULL,
    HeightCm DECIMAL(5,2) NULL,
    WeightKg DECIMAL(5,2) NULL,
    ActivityLevel NVARCHAR(50) NULL,
    HealthGoals NVARCHAR(MAX) NULL,
    PreferredCuisines NVARCHAR(MAX) NULL,
    DislikedFoods NVARCHAR(MAX) NULL,
    CookingSkillLevel NVARCHAR(50) NULL,
    SubscriptionTier NVARCHAR(50) NOT NULL DEFAULT 'Free',
    SubscriptionExpiresAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION
);
GO

CREATE INDEX IX_UserProfile_UserId ON UserProfile(UserId) WHERE IsDeleted = 0;
GO

-- Allergen Master Table
CREATE TABLE Allergen (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    AlternativeNames NVARCHAR(MAX) NULL,
    Description NVARCHAR(MAX) NULL,
    Category NVARCHAR(100) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT UQ_Allergen_Name UNIQUE (Name)
);
GO

-- Dietary Restriction Master Table
CREATE TABLE DietaryRestriction (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    Type NVARCHAR(50) NOT NULL, -- Medical, Religious, Ethical, Preference, Health
    Description NVARCHAR(MAX) NULL,
    CommonExclusions NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT UQ_DietaryRestriction_Name UNIQUE (Name)
);
GO

-- User Allergen Association
CREATE TABLE UserAllergen (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    AllergenId UNIQUEIDENTIFIER NOT NULL,
    Severity NVARCHAR(50) NOT NULL, -- Mild, Moderate, Severe, Anaphylaxis
    Notes NVARCHAR(MAX) NULL,
    DiagnosedDate DATE NULL,
    VerifiedByDoctor BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_UserAllergen_Allergen FOREIGN KEY (AllergenId)
        REFERENCES Allergen(Id),
    CONSTRAINT UQ_UserAllergen_User_Allergen UNIQUE (UserId, AllergenId)
);
GO

CREATE INDEX IX_UserAllergen_UserId ON UserAllergen(UserId) WHERE IsDeleted = 0;
CREATE INDEX IX_UserAllergen_AllergenId ON UserAllergen(AllergenId) WHERE IsDeleted = 0;
GO

-- User Dietary Restriction Association
CREATE TABLE UserDietaryRestriction (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    DietaryRestrictionId UNIQUEIDENTIFIER NOT NULL,
    Strictness NVARCHAR(50) NOT NULL, -- Strict, Moderate, Flexible
    Notes NVARCHAR(MAX) NULL,
    StartDate DATE NULL,
    EndDate DATE NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_UserDietaryRestriction_DietaryRestriction FOREIGN KEY (DietaryRestrictionId)
        REFERENCES DietaryRestriction(Id),
    CONSTRAINT UQ_UserDietaryRestriction_User_Restriction UNIQUE (UserId, DietaryRestrictionId)
);
GO

CREATE INDEX IX_UserDietaryRestriction_UserId ON UserDietaryRestriction(UserId) WHERE IsDeleted = 0;
CREATE INDEX IX_UserDietaryRestriction_RestrictionId ON UserDietaryRestriction(DietaryRestrictionId) WHERE IsDeleted = 0;
GO

-- Family Member Table
CREATE TABLE FamilyMember (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    PrimaryUserId UNIQUEIDENTIFIER NOT NULL,
    Name NVARCHAR(100) NOT NULL,
    Relationship NVARCHAR(50) NULL,
    DateOfBirth DATE NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION
);
GO

CREATE INDEX IX_FamilyMember_PrimaryUserId ON FamilyMember(PrimaryUserId) WHERE IsDeleted = 0;
GO

-- Family Member Allergen Association
CREATE TABLE FamilyMemberAllergen (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FamilyMemberId UNIQUEIDENTIFIER NOT NULL,
    AllergenId UNIQUEIDENTIFIER NOT NULL,
    Severity NVARCHAR(50) NOT NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_FamilyMemberAllergen_FamilyMember FOREIGN KEY (FamilyMemberId)
        REFERENCES FamilyMember(Id) ON DELETE CASCADE,
    CONSTRAINT FK_FamilyMemberAllergen_Allergen FOREIGN KEY (AllergenId)
        REFERENCES Allergen(Id),
    CONSTRAINT UQ_FamilyMemberAllergen_Member_Allergen UNIQUE (FamilyMemberId, AllergenId)
);
GO

CREATE INDEX IX_FamilyMemberAllergen_FamilyMemberId ON FamilyMemberAllergen(FamilyMemberId) WHERE IsDeleted = 0;
CREATE INDEX IX_FamilyMemberAllergen_AllergenId ON FamilyMemberAllergen(AllergenId) WHERE IsDeleted = 0;
GO

-- Family Member Dietary Restriction Association
CREATE TABLE FamilyMemberDietaryRestriction (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FamilyMemberId UNIQUEIDENTIFIER NOT NULL,
    DietaryRestrictionId UNIQUEIDENTIFIER NOT NULL,
    Strictness NVARCHAR(50) NOT NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy UNIQUEIDENTIFIER NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy UNIQUEIDENTIFIER NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    RowVersion ROWVERSION,
    CONSTRAINT FK_FamilyMemberDietaryRestriction_FamilyMember FOREIGN KEY (FamilyMemberId)
        REFERENCES FamilyMember(Id) ON DELETE CASCADE,
    CONSTRAINT FK_FamilyMemberDietaryRestriction_DietaryRestriction FOREIGN KEY (DietaryRestrictionId)
        REFERENCES DietaryRestriction(Id),
    CONSTRAINT UQ_FamilyMemberDietaryRestriction_Member_Restriction UNIQUE (FamilyMemberId, DietaryRestrictionId)
);
GO

CREATE INDEX IX_FamilyMemberDietaryRestriction_FamilyMemberId ON FamilyMemberDietaryRestriction(FamilyMemberId) WHERE IsDeleted = 0;
CREATE INDEX IX_FamilyMemberDietaryRestriction_RestrictionId ON FamilyMemberDietaryRestriction(DietaryRestrictionId) WHERE IsDeleted = 0;
GO
