CREATE TABLE [dbo].[DeviceUniqueIdentifier] (
    [ID]                 INT IDENTITY (1, 1) NOT NULL,
    [DeviceID]           INT NOT NULL,
    [UniqueIdentifierID] INT NOT NULL,
    [UseDatesID]         INT NOT NULL,
    CONSTRAINT [PK_DeviceUniqueIdentifier] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_DeviceUniqueIdentifier_Device] FOREIGN KEY ([DeviceID]) REFERENCES [dbo].[Device] ([ID]),
    CONSTRAINT [FK_DeviceUniqueIdentifier_UniqueIdentifier] FOREIGN KEY ([UniqueIdentifierID]) REFERENCES [dbo].[UniqueIdentifier] ([ID]),
    CONSTRAINT [FK_DeviceUniqueIdentifier_UseDates] FOREIGN KEY ([UseDatesID]) REFERENCES [dbo].[UseDates] ([ID])
);

