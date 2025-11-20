CREATE TABLE [dbo].[MilitaryBranch] (
    [ID]                INT        IDENTITY (1, 1) NOT NULL,
    [Name]              NCHAR (10) NULL,
    [MilitaryCountryID] INT        NULL,
    CONSTRAINT [PK_MilitaryBranch] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_MilitaryBranch_MilitaryCountry] FOREIGN KEY ([MilitaryCountryID]) REFERENCES [dbo].[MilitaryCountry] ([ID])
);

