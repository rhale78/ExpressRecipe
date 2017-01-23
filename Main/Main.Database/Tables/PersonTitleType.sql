CREATE TABLE [dbo].[PersonTitleType] (
    [ID]          INT           IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (50) NULL,
    [Description] NVARCHAR (50) NULL,
    CONSTRAINT [PK_PersonTitleType] PRIMARY KEY CLUSTERED ([ID] ASC)
);

