CREATE TABLE [dbo].[PersonNameSuffix] (
    [ID]           INT           IDENTITY (1, 1) NOT NULL,
    [Abbreviation] NVARCHAR (10) NULL,
    [Description]  NVARCHAR (50) NULL,
    CONSTRAINT [PK_PersonNameSuffix] PRIMARY KEY CLUSTERED ([ID] ASC)
);

