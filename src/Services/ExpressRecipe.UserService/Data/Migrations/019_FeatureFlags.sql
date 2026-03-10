-- Migration: 019_FeatureFlags
-- Description: Feature flag tables for PAY3B gate enforcement
-- Supports: global admin toggle, per-user overrides, subscription-tier requirements

-- Feature flag definitions (admin-managed)
IF OBJECT_ID('FeatureFlag', 'U') IS NULL
BEGIN
    CREATE TABLE FeatureFlag (
        Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        FeatureKey        NVARCHAR(100)    NOT NULL,
        Description       NVARCHAR(500)    NULL,
        IsEnabled         BIT              NOT NULL DEFAULT 1,
        RolloutPercentage INT              NOT NULL DEFAULT 100
            CONSTRAINT CK_FeatureFlag_RolloutPercentage CHECK (RolloutPercentage BETWEEN 0 AND 100),
        RequiredTier      NVARCHAR(50)     NULL,   -- NULL = no tier restriction
        CreatedAt         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt         DATETIME2        NULL,
        CONSTRAINT UQ_FeatureFlag_FeatureKey UNIQUE (FeatureKey)
    );

    CREATE INDEX IX_FeatureFlag_IsEnabled ON FeatureFlag (IsEnabled);
END
GO

-- Per-user overrides (beta access grants / explicit revocations)
IF OBJECT_ID('UserFeatureFlagOverride', 'U') IS NULL
BEGIN
    CREATE TABLE UserFeatureFlagOverride (
        Id         UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        UserId     UNIQUEIDENTIFIER NOT NULL,
        FeatureKey NVARCHAR(100)    NOT NULL,
        IsEnabled  BIT              NOT NULL DEFAULT 1,
        ExpiresAt  DATETIME2        NULL,   -- NULL = permanent until manually revoked
        CreatedAt  DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_UserFeatureFlagOverride UNIQUE (UserId, FeatureKey)
    );

    CREATE INDEX IX_UserFeatureFlagOverride_UserId     ON UserFeatureFlagOverride (UserId);
    CREATE INDEX IX_UserFeatureFlagOverride_FeatureKey ON UserFeatureFlagOverride (FeatureKey);
    CREATE INDEX IX_UserFeatureFlagOverride_ExpiresAt  ON UserFeatureFlagOverride (ExpiresAt)
        WHERE ExpiresAt IS NOT NULL;
END
GO

-- Seed well-known feature keys so the admin UI is populated on first run
IF NOT EXISTS (SELECT 1 FROM FeatureFlag WHERE FeatureKey = 'allergy-engine')
    INSERT INTO FeatureFlag (FeatureKey, Description, IsEnabled, RolloutPercentage, RequiredTier)
    VALUES ('allergy-engine', 'Allergy incident logging and alerts', 1, 100, NULL);
GO

IF NOT EXISTS (SELECT 1 FROM FeatureFlag WHERE FeatureKey = 'inventory-tracking')
    INSERT INTO FeatureFlag (FeatureKey, Description, IsEnabled, RolloutPercentage, RequiredTier)
    VALUES ('inventory-tracking', 'Household inventory management', 1, 100, 'Plus');
GO

IF NOT EXISTS (SELECT 1 FROM FeatureFlag WHERE FeatureKey = 'pantry-discovery')
    INSERT INTO FeatureFlag (FeatureKey, Description, IsEnabled, RolloutPercentage, RequiredTier)
    VALUES ('pantry-discovery', 'AI-powered pantry discovery suggestions', 1, 100, NULL);
GO

IF NOT EXISTS (SELECT 1 FROM FeatureFlag WHERE FeatureKey = 'advanced-reports')
    INSERT INTO FeatureFlag (FeatureKey, Description, IsEnabled, RolloutPercentage, RequiredTier)
    VALUES ('advanced-reports', 'Advanced analytics and reporting', 1, 100, 'Plus');
GO

IF NOT EXISTS (SELECT 1 FROM FeatureFlag WHERE FeatureKey = 'homestead')
    INSERT INTO FeatureFlag (FeatureKey, Description, IsEnabled, RolloutPercentage, RequiredTier)
    VALUES ('homestead', 'Livestock and homestead tracking', 1, 100, 'Premium');
GO

IF NOT EXISTS (SELECT 1 FROM FeatureFlag WHERE FeatureKey = 'ai-recipe-suggestions')
    INSERT INTO FeatureFlag (FeatureKey, Description, IsEnabled, RolloutPercentage, RequiredTier)
    VALUES ('ai-recipe-suggestions', 'AI-powered personalized recipe suggestions', 0, 0, 'Plus');
GO
