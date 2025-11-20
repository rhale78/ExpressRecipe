CREATE TABLE [dbo].[StateCounty] (
    [ID]            INT IDENTITY (1, 1) NOT NULL,
    [StateCountyID] INT NULL,
    [CountyID]      INT NULL,
    CONSTRAINT [PK_StateCounty] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_StateCounty_County] FOREIGN KEY ([CountyID]) REFERENCES [dbo].[County] ([ID]),
    CONSTRAINT [FK_StateCounty_StateCountry] FOREIGN KEY ([StateCountyID]) REFERENCES [dbo].[StateCountry] ([ID])
);

