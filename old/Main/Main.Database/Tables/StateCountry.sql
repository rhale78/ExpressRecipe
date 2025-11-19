CREATE TABLE [dbo].[StateCountry] (
    [ID]                 INT           IDENTITY (1, 1) NOT NULL,
    [StateProvidenceID]  INT           NULL,
    [CountryID]          INT           NULL,
    [Abbreviation]       NVARCHAR (25) NULL,
    [CapitalCityStateID] INT           NULL,
    CONSTRAINT [PK_StateCountry] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_StateCountry_CityState] FOREIGN KEY ([CapitalCityStateID]) REFERENCES [dbo].[CityState] ([ID]),
    CONSTRAINT [FK_StateCountry_Country] FOREIGN KEY ([CountryID]) REFERENCES [dbo].[Country] ([ID]),
    CONSTRAINT [FK_StateCountry_StateProvidence] FOREIGN KEY ([StateProvidenceID]) REFERENCES [dbo].[StateProvidence] ([ID])
);

