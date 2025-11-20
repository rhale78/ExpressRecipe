CREATE TABLE [dbo].[DeviceInstance] (
    [ID]         INT IDENTITY (1, 1) NOT NULL,
    [DeviceID]   INT NOT NULL,
    [UseDatesID] INT NOT NULL,
    CONSTRAINT [PK_DeviceInstance] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_DeviceInstance_Device] FOREIGN KEY ([DeviceID]) REFERENCES [dbo].[Device] ([ID]),
    CONSTRAINT [FK_DeviceInstance_UseDates] FOREIGN KEY ([UseDatesID]) REFERENCES [dbo].[UseDates] ([ID])
);

