CREATE TABLE [dbo].[PersonReligion] (
    [ID]          INT        IDENTITY (1, 1) NOT NULL,
    [Name]        NCHAR (10) NULL,
    [Description] NCHAR (10) NULL,
    CONSTRAINT [PK_PersonReligion] PRIMARY KEY CLUSTERED ([ID] ASC)
);

