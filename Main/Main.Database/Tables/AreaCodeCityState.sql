CREATE TABLE [dbo].[AreaCodeCityState] (
    [ID]          INT IDENTITY (1, 1) NOT NULL,
    [AreaCodeID]  INT NULL,
    [CityStateID] INT NULL,
    CONSTRAINT [PK_AreaCodeCityState] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_AreaCodeCityState_AreaCode] FOREIGN KEY ([AreaCodeID]) REFERENCES [dbo].[AreaCode] ([ID]),
    CONSTRAINT [FK_AreaCodeCityState_CityState] FOREIGN KEY ([CityStateID]) REFERENCES [dbo].[CityState] ([ID])
);

