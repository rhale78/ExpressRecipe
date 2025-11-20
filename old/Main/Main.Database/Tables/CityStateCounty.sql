CREATE TABLE [dbo].[CityStateCounty] (
    [ID]            INT IDENTITY (1, 1) NOT NULL,
    [CityStateID]   INT NULL,
    [StateCountyID] INT NULL,
    CONSTRAINT [PK_CityStateCounty] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_CityStateCounty_CityState] FOREIGN KEY ([CityStateID]) REFERENCES [dbo].[CityState] ([ID]),
    CONSTRAINT [FK_CityStateCounty_StateCounty] FOREIGN KEY ([StateCountyID]) REFERENCES [dbo].[StateCounty] ([ID])
);

