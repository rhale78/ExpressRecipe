-- Migration: 019_FeatureFlags
-- Description: Feature flag system — global kill switches + per-user overrides

CREATE TABLE FeatureFlag (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    FeatureKey      NVARCHAR(100) NOT NULL,
    IsEnabled       BIT NOT NULL DEFAULT 1,
    RolloutPercent  INT NOT NULL DEFAULT 100,   -- 0-100; 100=everyone, 0=no one
    RequiresTier    NVARCHAR(20) NULL,           -- NULL=any tier; 'Plus'|'Premium'|'AdFree'
    Description     NVARCHAR(500) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       UNIQUEIDENTIFIER NULL,
    UpdatedAt       DATETIME2 NULL,
    UpdatedBy       UNIQUEIDENTIFIER NULL,
    IsDeleted       BIT NOT NULL DEFAULT 0,
    DeletedAt       DATETIME2 NULL,
    RowVersion      ROWVERSION,
    CONSTRAINT UQ_FeatureFlag_Key UNIQUE (FeatureKey),
    CONSTRAINT CK_FeatureFlag_RolloutPercent CHECK (RolloutPercent BETWEEN 0 AND 100),
    CONSTRAINT CK_FeatureFlag_RequiresTier CHECK (RequiresTier IS NULL OR RequiresTier IN ('Free', 'AdFree', 'Plus', 'Premium'))
);
CREATE INDEX IX_FeatureFlag_IsEnabled ON FeatureFlag(IsEnabled) WHERE IsDeleted = 0;
GO

CREATE TABLE UserFeatureOverride (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId      UNIQUEIDENTIFIER NOT NULL,
    FeatureKey  NVARCHAR(100) NOT NULL,
    IsEnabled   BIT NOT NULL,           -- true=force-on; false=force-off
    Reason      NVARCHAR(200) NULL,     -- e.g. "Beta tester", "CS credit", "QA"
    GrantedBy   UNIQUEIDENTIFIER NULL,  -- admin userId
    ExpiresAt   DATETIME2 NULL,         -- null = permanent
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy   UNIQUEIDENTIFIER NULL,
    UpdatedAt   DATETIME2 NULL,
    UpdatedBy   UNIQUEIDENTIFIER NULL,
    IsDeleted   BIT NOT NULL DEFAULT 0,
    DeletedAt   DATETIME2 NULL,
    RowVersion  ROWVERSION,
    CONSTRAINT UQ_UserFeatureOverride_User_Key UNIQUE (UserId, FeatureKey)
);
CREATE INDEX IX_UserFeatureOverride_UserId     ON UserFeatureOverride(UserId)     WHERE IsDeleted = 0;
CREATE INDEX IX_UserFeatureOverride_FeatureKey ON UserFeatureOverride(FeatureKey) WHERE IsDeleted = 0;
GO

-- Seed known feature keys (IsEnabled defaults to 1 = on locally, can be toggled in admin)
INSERT INTO FeatureFlag (FeatureKey, Description, RequiresTier) VALUES
('allergy-engine',        'Allergy incident logging and differential analysis', NULL),
('allergy-reporting',     'Printable doctor allergy report (PDF)', NULL),
('pantry-discovery',      'What Can I Make Right Now discovery page', NULL),
('meal-planning',         'Full meal planning features', NULL),
('inventory-tracking',    'Inventory management and tracking', 'Plus'),
('price-tracking',        'Price comparison and deal alerts', 'Plus'),
('advanced-reports',      'Analytics and spending reports', 'Premium'),
('homestead',             'Livestock and homestead production tracking', 'Premium'),
('equipment-tracking',    'Kitchen equipment and storage management', NULL),
('cooking-timers',        'Built-in cooking timers with notifications', NULL),
('recipe-import',         'Import recipes from URLs', 'Plus'),
('community-gallery',     'Browse and publish community recipes', NULL),
('ai-recipe-suggestions', 'AI-powered recipe personalization', 'Plus'),
('ads-display',           'Show advertising (disabled for AdFree+ tiers)', NULL);
GO
