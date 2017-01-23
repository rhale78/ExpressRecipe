CREATE TABLE [dbo].[PersonNameDegree] (
    [ID]                INT           IDENTITY (1, 1) NOT NULL,
    [Abbreviation]      NVARCHAR (10) NULL,
    [Description]       NVARCHAR (50) NULL,
    [PersonTitleTypeID] INT           NULL,
    CONSTRAINT [PK_PersonNameDegree] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonNameDegree_PersonTitleType] FOREIGN KEY ([PersonTitleTypeID]) REFERENCES [dbo].[PersonTitleType] ([ID])
);

