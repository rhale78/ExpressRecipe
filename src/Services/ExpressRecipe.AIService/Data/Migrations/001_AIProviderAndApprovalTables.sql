-- Migration: 001_AIProviderAndApprovalTables
-- Description: Create AI provider config and approval queue tables for AIService
-- Date: 2026-03-10

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AIProviderConfig')
BEGIN
    CREATE TABLE AIProviderConfig (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        UseCase     NVARCHAR(100)    NOT NULL,
        Provider    NVARCHAR(50)     NOT NULL DEFAULT 'Ollama',
        CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt   DATETIME2        NULL,
        IsDeleted   BIT              NOT NULL DEFAULT 0,
        DeletedAt   DATETIME2        NULL,

        CONSTRAINT UQ_AIProviderConfig_UseCase UNIQUE (UseCase)
    );

    CREATE INDEX IX_AIProviderConfig_UseCase ON AIProviderConfig(UseCase)
        WHERE IsDeleted = 0;

    -- Seed default configs
    INSERT INTO AIProviderConfig (UseCase, Provider)
    VALUES
        ('global',           'Ollama'),
        ('recipe-approval',  'Ollama'),
        ('product-approval', 'Ollama');
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ApprovalConfig')
BEGIN
    CREATE TABLE ApprovalConfig (
        Id               UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        EntityType       NVARCHAR(100)    NOT NULL,
        Mode             NVARCHAR(50)     NOT NULL DEFAULT 'HumanFirst',
        AIConfidenceMin  DECIMAL(5, 4)    NOT NULL DEFAULT 0.75,
        HumanTimeoutMins INT              NOT NULL DEFAULT 120,
        CreatedAt        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt        DATETIME2        NULL,
        IsDeleted        BIT              NOT NULL DEFAULT 0,
        DeletedAt        DATETIME2        NULL,

        CONSTRAINT UQ_ApprovalConfig_EntityType UNIQUE (EntityType),
        CONSTRAINT CK_ApprovalConfig_Mode CHECK (Mode IN ('AIFirst', 'AIOnly', 'HumanFirst', 'HumanOnly')),
        CONSTRAINT CK_ApprovalConfig_AIConfidenceMin CHECK (AIConfidenceMin BETWEEN 0.0 AND 1.0)
    );

    -- Seed default approval configs
    INSERT INTO ApprovalConfig (EntityType, Mode, AIConfidenceMin, HumanTimeoutMins)
    VALUES
        ('Recipe',  'AIFirst',   0.75, 120),
        ('Product', 'HumanFirst', 0.75, 120);
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ApprovalQueue')
BEGIN
    CREATE TABLE ApprovalQueue (
        Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        EntityId    UNIQUEIDENTIFIER NOT NULL,
        EntityType  NVARCHAR(100)    NOT NULL,
        Mode        NVARCHAR(50)     NOT NULL DEFAULT 'HumanFirst',
        Status      NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
        Content     NVARCHAR(MAX)    NULL,
        AIReason    NVARCHAR(MAX)    NULL,
        CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt   DATETIME2        NULL,
        IsDeleted   BIT              NOT NULL DEFAULT 0,
        DeletedAt   DATETIME2        NULL,

        CONSTRAINT CK_ApprovalQueue_Status
            CHECK (Status IN ('Pending', 'PendingHuman', 'Approved', 'Rejected'))
    );

    CREATE INDEX IX_ApprovalQueue_EntityId     ON ApprovalQueue(EntityId)    WHERE IsDeleted = 0;
    CREATE INDEX IX_ApprovalQueue_EntityType   ON ApprovalQueue(EntityType)  WHERE IsDeleted = 0;
    CREATE INDEX IX_ApprovalQueue_Status       ON ApprovalQueue(Status)      WHERE IsDeleted = 0;
END
GO
