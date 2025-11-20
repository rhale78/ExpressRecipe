CREATE TABLE [dbo].[StreetToType] (
    [ID]           INT IDENTITY (1, 1) NOT NULL,
    [StreetID]     INT NULL,
    [StreetTypeID] INT NULL,
    CONSTRAINT [PK_StreetToType] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_StreetToType_Street] FOREIGN KEY ([StreetID]) REFERENCES [dbo].[Street] ([ID]),
    CONSTRAINT [FK_StreetToType_StreetType] FOREIGN KEY ([StreetTypeID]) REFERENCES [dbo].[StreetType] ([ID])
);

