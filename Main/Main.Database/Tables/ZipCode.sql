CREATE TABLE [dbo].[ZipCode] (
    [ID]                INT           IDENTITY (1, 1) NOT NULL,
    [ZipCode]           NVARCHAR (10) NULL,
    [CityStateCountyID] INT           NULL,
    CONSTRAINT [PK_ZipCode] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_ZipCode_CityStateCounty] FOREIGN KEY ([CityStateCountyID]) REFERENCES [dbo].[CityStateCounty] ([ID])
);

