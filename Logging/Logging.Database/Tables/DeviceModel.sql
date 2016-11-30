CREATE TABLE [dbo].[DeviceModel] (
    [ID]          INT            IDENTITY (1, 1) NOT NULL,
    [ModelNumber] NVARCHAR (75)  NOT NULL,
    [Description] NVARCHAR (255) NULL,
    CONSTRAINT [PK_DeviceModel] PRIMARY KEY CLUSTERED ([ID] ASC)
);

