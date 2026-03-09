IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AllergenProfile')
BEGIN
CREATE TABLE AllergenProfile (
    Id                   UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MemberId             UNIQUEIDENTIFIER NOT NULL,
    AllergenId           UNIQUEIDENTIFIER NULL,
    FreeFormName         NVARCHAR(200) NULL,
    FreeFormBrand        NVARCHAR(200) NULL,
    LinkedIngredientId   UNIQUEIDENTIFIER NULL,
    LinkedProductId      UNIQUEIDENTIFIER NULL,
    IsUnresolved         BIT NOT NULL DEFAULT 0,
    ResolvedAt           DATETIME2 NULL,
    ExposureThreshold    NVARCHAR(50) NOT NULL DEFAULT 'IngestionOnly',
    Severity             NVARCHAR(50) NOT NULL DEFAULT 'Moderate',
    HouseholdExclude     BIT NOT NULL DEFAULT 0,
    CreatedAt            DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy            UNIQUEIDENTIFIER NULL,
    UpdatedAt            DATETIME2 NULL,
    IsDeleted            BIT NOT NULL DEFAULT 0,
    RowVersion           ROWVERSION,
    CONSTRAINT CK_AllergenProfile_OneSource CHECK (
        (AllergenId IS NOT NULL AND FreeFormName IS NULL) OR
        (AllergenId IS NULL     AND FreeFormName IS NOT NULL)),
    CONSTRAINT CK_AllergenProfile_Threshold CHECK (ExposureThreshold IN
        ('IngestionOnly','ContactSensitive','AirborneSensitive')),
    CONSTRAINT CK_AllergenProfile_Severity CHECK (Severity IN
        ('LifeThreatening','Severe','Moderate','Mild'))
);
CREATE INDEX IX_AllergenProfile_MemberId ON AllergenProfile(MemberId) WHERE IsDeleted = 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AllergenProfileLink')
BEGIN
CREATE TABLE AllergenProfileLink (
    Id                 UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    AllergenProfileId  UNIQUEIDENTIFIER NOT NULL,
    LinkType           NVARCHAR(50) NOT NULL,
    LinkedId           UNIQUEIDENTIFIER NOT NULL,
    MatchMethod        NVARCHAR(50) NOT NULL,
    ConfidenceScore    DECIMAL(4,3) NOT NULL DEFAULT 1.000,
    CreatedAt          DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_AllergenProfileLink_Profile FOREIGN KEY (AllergenProfileId)
        REFERENCES AllergenProfile(Id) ON DELETE CASCADE,
    CONSTRAINT CK_AllergenProfileLink_Type CHECK (LinkType IN
        ('Ingredient','Product','MenuItemIngredient'))
);
CREATE INDEX IX_AllergenProfileLink_ProfileId ON AllergenProfileLink(AllergenProfileId);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TemporarySchedule')
BEGIN
CREATE TABLE TemporarySchedule (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MemberId     UNIQUEIDENTIFIER NOT NULL,
    ScheduleType NVARCHAR(50) NOT NULL,
    ActiveFrom   DATETIME2 NOT NULL,
    ActiveUntil  DATETIME2 NOT NULL,
    ConfigJson   NVARCHAR(MAX) NULL,
    CreatedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2 NULL,
    IsDeleted    BIT NOT NULL DEFAULT 0,
    CONSTRAINT CK_TemporarySchedule_Type CHECK (ScheduleType IN (
        'Pregnancy','Menstrual','AthleticTraining','ReligiousFasting',
        'PostSurgery','EliminationDietTrial','Custom')),
    CONSTRAINT CK_TemporarySchedule_Dates CHECK (ActiveUntil > ActiveFrom)
);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AdaptationOverride')
BEGIN
CREATE TABLE AdaptationOverride (
    Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId      UNIQUEIDENTIFIER NOT NULL,
    RecipeInstanceId UNIQUEIDENTIFIER NULL,
    MemberId         UNIQUEIDENTIFIER NULL,
    StrategyCode     NVARCHAR(50) NOT NULL,
    CreatedAt        DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy        UNIQUEIDENTIFIER NULL,
    UpdatedAt        DATETIME2 NULL,
    CONSTRAINT CK_AdaptationOverride_Strategy CHECK (StrategyCode IN
        ('AdaptAll','SplitMeal','SeparateMeal'))
);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MemberOnboardingSagaState')
BEGIN
CREATE TABLE MemberOnboardingSagaState (
    CorrelationId   NVARCHAR(200) NOT NULL PRIMARY KEY,
    CurrentMask     BIGINT NOT NULL DEFAULT 0,
    StartedAt       DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CompletedAt     DATETIMEOFFSET NULL,
    Status          NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    HouseholdId     UNIQUEIDENTIFIER NOT NULL,
    MemberType      NVARCHAR(50) NOT NULL,
    DisplayName     NVARCHAR(100) NOT NULL,
    MemberId        UNIQUEIDENTIFIER NULL,
    LastError       NVARCHAR(MAX) NULL
);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AllergenResolutionSagaState')
BEGIN
CREATE TABLE AllergenResolutionSagaState (
    CorrelationId      NVARCHAR(200) NOT NULL PRIMARY KEY,
    CurrentMask        BIGINT NOT NULL DEFAULT 0,
    StartedAt          DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CompletedAt        DATETIMEOFFSET NULL,
    Status             NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    AllergenProfileId  UNIQUEIDENTIFIER NOT NULL,
    MemberId           UNIQUEIDENTIFIER NOT NULL,
    FreeFormText       NVARCHAR(200) NOT NULL,
    Brand              NVARCHAR(200) NULL,
    IngredientId       UNIQUEIDENTIFIER NULL,
    ProductId          UNIQUEIDENTIFIER NULL,
    MatchMethod        NVARCHAR(50) NULL,
    LinksWritten       INT NOT NULL DEFAULT 0,
    LastError          NVARCHAR(MAX) NULL
);
END
