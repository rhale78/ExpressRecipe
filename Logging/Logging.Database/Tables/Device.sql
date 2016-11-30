CREATE TABLE [dbo].[Device] (
    [ID]            INT IDENTITY (1, 1) NOT NULL,
    [DeviceTypeID]  INT NOT NULL,
    [BrandID]       INT NOT NULL,
    [DeviceModelID] INT NOT NULL,
    [OSVersionID]   INT NOT NULL,
    CONSTRAINT [PK_Device] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_Device_Brand] FOREIGN KEY ([BrandID]) REFERENCES [dbo].[Brand] ([ID]),
    CONSTRAINT [FK_Device_DeviceModel] FOREIGN KEY ([DeviceModelID]) REFERENCES [dbo].[DeviceModel] ([ID]),
    CONSTRAINT [FK_Device_DeviceType] FOREIGN KEY ([DeviceTypeID]) REFERENCES [dbo].[DeviceType] ([ID]),
    CONSTRAINT [FK_Device_OSVersion] FOREIGN KEY ([OSVersionID]) REFERENCES [dbo].[OSVersion] ([ID])
);

