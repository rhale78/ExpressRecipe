-- Migration: 019_RewardCatalogSeed
-- Description: Seed specific reward catalog items and add UserBadge table
-- Date: 2026-03-10

-- Add UserBadge table
CREATE TABLE UserBadge (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId      UNIQUEIDENTIFIER NOT NULL,
    BadgeCode   NVARCHAR(100) NOT NULL,
    AwardedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_UserBadge_User_Code UNIQUE (UserId, BadgeCode)
);

CREATE INDEX IX_UserBadge_UserId ON UserBadge(UserId);
GO

-- Seed specific reward catalog items from COM1 spec
INSERT INTO RewardItem (Name, Description, PointsCost, RewardType, Value, IsActive) VALUES
('1 Premium Day',     'Unlock Premium features for 1 day',   200, 'SubscriptionExtension', '1d',  1),
('1 Week Ad-Free',    'No ads for 7 days',                   300, 'SubscriptionExtension', '7d',  1),
('1 Month Plus',      'Full Plus access for 1 month',       2000, 'SubscriptionExtension', '30d', 1),
('Early Access Badge','Beta tester community badge',          500, 'Badge', 'BetaTester',  1),
('Power User Badge',  'Awarded for 500 lifetime points',     500, 'Badge', 'PowerUser',   1);
GO
