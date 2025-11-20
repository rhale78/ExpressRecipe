CREATE TABLE [dbo].[CityState] (
    [ID]                INT IDENTITY (1, 1) NOT NULL,
    [CityID]            INT NULL,
    [StateProvidenceID] INT NULL,
    CONSTRAINT [PK_CityState] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_CityState_City] FOREIGN KEY ([CityID]) REFERENCES [dbo].[City] ([ID]),
    CONSTRAINT [FK_CityState_StateProvidence] FOREIGN KEY ([StateProvidenceID]) REFERENCES [dbo].[StateProvidence] ([ID])
);

