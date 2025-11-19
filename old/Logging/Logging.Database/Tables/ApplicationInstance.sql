CREATE TABLE [dbo].[ApplicationInstance] (
    [ID]                   INT      IDENTITY (1, 1) NOT NULL,
    [ApplicationVersionID] INT      NOT NULL,
    [StatusTypeID]         INT      NOT NULL,
    [UpgradeDate]          DATETIME NULL,
    [UpgradeToVersionID]   INT      NULL,
    [DeviceID]             INT      NOT NULL,
    [UserID]               INT      NULL,
    [UseDatesID]           INT      NULL,
    CONSTRAINT [PK_ApplicationInstance] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_ApplicationInstance_ApplicationVersion] FOREIGN KEY ([ApplicationVersionID]) REFERENCES [dbo].[ApplicationVersion] ([ID]),
    CONSTRAINT [FK_ApplicationInstance_ApplicationVersion1] FOREIGN KEY ([UpgradeToVersionID]) REFERENCES [dbo].[ApplicationVersion] ([ID]),
    CONSTRAINT [FK_ApplicationInstance_Device] FOREIGN KEY ([DeviceID]) REFERENCES [dbo].[Device] ([ID]),
    CONSTRAINT [FK_ApplicationInstance_StatusType] FOREIGN KEY ([StatusTypeID]) REFERENCES [dbo].[StatusType] ([ID]),
    CONSTRAINT [FK_ApplicationInstance_UseDates] FOREIGN KEY ([UseDatesID]) REFERENCES [dbo].[UseDates] ([ID]),
    CONSTRAINT [FK_ApplicationInstance_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([ID])
);

