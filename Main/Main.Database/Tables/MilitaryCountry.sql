CREATE TABLE [dbo].[MilitaryCountry] (
    [ID]        INT IDENTITY (1, 1) NOT NULL,
    [CountryID] INT NULL,
    CONSTRAINT [PK_MilitaryCountry] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_MilitaryCountry_Country] FOREIGN KEY ([CountryID]) REFERENCES [dbo].[Country] ([ID])
);

