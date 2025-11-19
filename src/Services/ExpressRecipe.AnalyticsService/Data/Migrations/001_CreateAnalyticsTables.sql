-- Migration: 001_CreateAnalyticsTables
-- Description: Create usage analytics and pattern detection tables
-- Date: 2024-11-19

CREATE TABLE UserEvent (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    EventType NVARCHAR(100) NOT NULL, -- PageView, Click, Scan, Purchase, etc.
    EventCategory NVARCHAR(100) NULL,
    EntityType NVARCHAR(100) NULL,
    EntityId UNIQUEIDENTIFIER NULL,
    Metadata NVARCHAR(MAX) NULL, -- JSON
    SessionId NVARCHAR(100) NULL,
    DeviceInfo NVARCHAR(500) NULL,
    Location NVARCHAR(500) NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_UserEvent_UserId ON UserEvent(UserId);
CREATE INDEX IX_UserEvent_EventType ON UserEvent(EventType);
CREATE INDEX IX_UserEvent_Timestamp ON UserEvent(Timestamp);
GO

CREATE TABLE UsageStatistics (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    MetricType NVARCHAR(100) NOT NULL,
    MetricValue DECIMAL(18, 6) NOT NULL,
    PeriodType NVARCHAR(50) NOT NULL, -- Daily, Weekly, Monthly
    PeriodStart DATE NOT NULL,
    PeriodEnd DATE NOT NULL,
    CalculatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT UQ_UsageStatistics UNIQUE (UserId, MetricType, PeriodType, PeriodStart)
);

CREATE INDEX IX_UsageStatistics_UserId ON UsageStatistics(UserId);
GO

CREATE TABLE PatternDetection (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    PatternType NVARCHAR(100) NOT NULL, -- ShoppingCycle, MealPreference, etc.
    PatternDescription NVARCHAR(MAX) NOT NULL,
    Confidence DECIMAL(5, 4) NOT NULL,
    Evidence NVARCHAR(MAX) NULL, -- JSON
    DetectedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsActive BIT NOT NULL DEFAULT 1,
    LastSeenAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE INDEX IX_PatternDetection_UserId ON PatternDetection(UserId);
GO

CREATE TABLE Insight (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    InsightType NVARCHAR(100) NOT NULL,
    Title NVARCHAR(300) NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    Priority NVARCHAR(50) NOT NULL DEFAULT 'Normal',
    ActionableItem NVARCHAR(MAX) NULL, -- JSON with action details
    IsViewed BIT NOT NULL DEFAULT 0,
    ViewedAt DATETIME2 NULL,
    IsDismissed BIT NOT NULL DEFAULT 0,
    DismissedAt DATETIME2 NULL,
    GeneratedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt DATETIME2 NULL
);

CREATE INDEX IX_Insight_UserId ON Insight(UserId);
GO

CREATE TABLE AggregateMetrics (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    MetricName NVARCHAR(100) NOT NULL,
    MetricValue DECIMAL(18, 6) NOT NULL,
    Dimensions NVARCHAR(MAX) NULL, -- JSON (category, region, etc.)
    AggregationType NVARCHAR(50) NOT NULL, -- Sum, Average, Count, etc.
    PeriodStart DATETIME2 NOT NULL,
    PeriodEnd DATETIME2 NOT NULL,
    CalculatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT UQ_AggregateMetrics UNIQUE (MetricName, AggregationType, PeriodStart)
);

CREATE INDEX IX_AggregateMetrics_MetricName ON AggregateMetrics(MetricName);
GO
