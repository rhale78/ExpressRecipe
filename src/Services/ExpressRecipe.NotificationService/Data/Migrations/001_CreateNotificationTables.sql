-- Migration: 001_CreateNotificationTables
-- Description: Create notification and delivery tracking tables
-- Date: 2024-11-19

CREATE TABLE Notification (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    Type NVARCHAR(50) NOT NULL, -- Expiration, Recall, Deal, System, Community
    Title NVARCHAR(300) NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    Priority NVARCHAR(50) NOT NULL DEFAULT 'Normal', -- Low, Normal, High, Urgent
    Category NVARCHAR(100) NULL,
    RelatedEntityType NVARCHAR(100) NULL, -- InventoryItem, Recall, Deal, etc.
    RelatedEntityId UNIQUEIDENTIFIER NULL,
    ActionUrl NVARCHAR(1000) NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    ReadAt DATETIME2 NULL,
    IsDismissed BIT NOT NULL DEFAULT 0,
    DismissedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt DATETIME2 NULL
);

CREATE INDEX IX_Notification_UserId ON Notification(UserId);
CREATE INDEX IX_Notification_CreatedAt ON Notification(CreatedAt);
CREATE INDEX IX_Notification_IsRead ON Notification(IsRead);
GO

CREATE TABLE NotificationPreference (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    NotificationType NVARCHAR(50) NOT NULL,
    EnableInApp BIT NOT NULL DEFAULT 1,
    EnableEmail BIT NOT NULL DEFAULT 1,
    EnablePush BIT NOT NULL DEFAULT 1,
    EnableSMS BIT NOT NULL DEFAULT 0,
    QuietHoursStart TIME NULL,
    QuietHoursEnd TIME NULL,
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT UQ_NotificationPreference_User_Type UNIQUE (UserId, NotificationType)
);

CREATE INDEX IX_NotificationPreference_UserId ON NotificationPreference(UserId);
GO

CREATE TABLE NotificationTemplate (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    Type NVARCHAR(50) NOT NULL,
    Channel NVARCHAR(50) NOT NULL, -- InApp, Email, Push, SMS
    Subject NVARCHAR(300) NULL,
    BodyTemplate NVARCHAR(MAX) NOT NULL,
    Variables NVARCHAR(MAX) NULL, -- JSON array of variable names
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT UQ_NotificationTemplate_Name_Channel UNIQUE (Name, Channel)
);
GO

CREATE TABLE DeliveryLog (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    NotificationId UNIQUEIDENTIFIER NOT NULL,
    Channel NVARCHAR(50) NOT NULL,
    Status NVARCHAR(50) NOT NULL, -- Pending, Sent, Failed, Bounced
    Recipient NVARCHAR(300) NULL,
    SentAt DATETIME2 NULL,
    DeliveredAt DATETIME2 NULL,
    FailureReason NVARCHAR(MAX) NULL,
    RetryCount INT NOT NULL DEFAULT 0,
    
    CONSTRAINT FK_DeliveryLog_Notification FOREIGN KEY (NotificationId)
        REFERENCES Notification(Id) ON DELETE CASCADE
);

CREATE INDEX IX_DeliveryLog_NotificationId ON DeliveryLog(NotificationId);
GO
