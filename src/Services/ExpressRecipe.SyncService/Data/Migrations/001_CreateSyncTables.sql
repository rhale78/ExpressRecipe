-- Migration: 001_CreateSyncTables
-- Description: Create local-first sync and conflict resolution tables
-- Date: 2024-11-19

CREATE TABLE SyncMetadata (
    EntityType NVARCHAR(100) NOT NULL,
    EntityId UNIQUEIDENTIFIER NOT NULL,
    UserId UNIQUEIDENTIFIER NOT NULL,
    LocalVersion INT NOT NULL DEFAULT 1,
    ServerVersion INT NOT NULL DEFAULT 0,
    LastSyncedAt DATETIME2 NULL,
    IsDirty BIT NOT NULL DEFAULT 0,
    IsDeleted BIT NOT NULL DEFAULT 0,
    DeletedAt DATETIME2 NULL,
    
    CONSTRAINT PK_SyncMetadata PRIMARY KEY (EntityType, EntityId)
);

CREATE INDEX IX_SyncMetadata_UserId ON SyncMetadata(UserId);
CREATE INDEX IX_SyncMetadata_IsDirty ON SyncMetadata(IsDirty);
GO

CREATE TABLE SyncConflict (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    EntityType NVARCHAR(100) NOT NULL,
    EntityId UNIQUEIDENTIFIER NOT NULL,
    ConflictType NVARCHAR(50) NOT NULL, -- UpdateUpdate, UpdateDelete, DeleteUpdate
    LocalData NVARCHAR(MAX) NOT NULL, -- JSON
    ServerData NVARCHAR(MAX) NOT NULL, -- JSON
    LocalVersion INT NOT NULL,
    ServerVersion INT NOT NULL,
    Resolution NVARCHAR(50) NULL, -- UseLocal, UseServer, Merge, Manual
    ResolvedData NVARCHAR(MAX) NULL,
    DetectedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ResolvedAt DATETIME2 NULL,
    ResolvedBy UNIQUEIDENTIFIER NULL
);

CREATE INDEX IX_SyncConflict_UserId ON SyncConflict(UserId);
CREATE INDEX IX_SyncConflict_Resolution ON SyncConflict(Resolution);
GO

CREATE TABLE SyncQueue (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    EntityType NVARCHAR(100) NOT NULL,
    EntityId UNIQUEIDENTIFIER NOT NULL,
    Operation NVARCHAR(50) NOT NULL, -- Create, Update, Delete
    Payload NVARCHAR(MAX) NOT NULL, -- JSON
    Priority INT NOT NULL DEFAULT 0,
    RetryCount INT NOT NULL DEFAULT 0,
    Status NVARCHAR(50) NOT NULL DEFAULT 'Pending', -- Pending, Processing, Completed, Failed
    ErrorMessage NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ProcessedAt DATETIME2 NULL
);

CREATE INDEX IX_SyncQueue_UserId_Status ON SyncQueue(UserId, Status);
CREATE INDEX IX_SyncQueue_Priority ON SyncQueue(Priority DESC);
GO

CREATE TABLE DeviceRegistration (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    DeviceId NVARCHAR(200) NOT NULL,
    DeviceName NVARCHAR(200) NULL,
    Platform NVARCHAR(50) NOT NULL, -- Web, Windows, Android, iOS
    AppVersion NVARCHAR(50) NULL,
    LastSyncAt DATETIME2 NULL,
    RegisteredAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    CONSTRAINT UQ_DeviceRegistration_DeviceId UNIQUE (DeviceId)
);

CREATE INDEX IX_DeviceRegistration_UserId ON DeviceRegistration(UserId);
GO
