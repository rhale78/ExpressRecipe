IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CookProfile')
BEGIN
CREATE TABLE CookProfile (
    Id                     UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MemberId               UNIQUEIDENTIFIER NOT NULL UNIQUE,
    CooksForHousehold      BIT NOT NULL DEFAULT 1,
    CookingFrequency       NVARCHAR(50) NOT NULL DEFAULT 'Regular',
    OverallSkillLevel      NVARCHAR(50) NOT NULL DEFAULT 'HomeCook',
    CookRole               NVARCHAR(50) NOT NULL DEFAULT 'PrimaryHomeChef',
    EatingDisorderRecovery BIT NOT NULL DEFAULT 0,
    IsDeleted              BIT NOT NULL DEFAULT 0,
    CreatedAt              DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt              DATETIME2 NULL,
    RowVersion             ROWVERSION,
    CONSTRAINT CK_CookProfile_Skill CHECK (OverallSkillLevel IN
        ('Beginner','HomeCook','ConfidentCook','Advanced','Professional'))
);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TechniqueComfort')
BEGIN
CREATE TABLE TechniqueComfort (
    Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MemberId      UNIQUEIDENTIFIER NOT NULL,
    TechniqueCode NVARCHAR(100) NOT NULL,
    ComfortLevel  NVARCHAR(50) NOT NULL,
    CreatedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt     DATETIME2 NULL,
    CONSTRAINT UQ_TechniqueComfort UNIQUE (MemberId, TechniqueCode)
);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'DismissedTip')
BEGIN
CREATE TABLE DismissedTip (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MemberId    UNIQUEIDENTIFIER NOT NULL,
    TipId       UNIQUEIDENTIFIER NOT NULL,
    DismissedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_DismissedTip UNIQUE (MemberId, TipId)
);
END
