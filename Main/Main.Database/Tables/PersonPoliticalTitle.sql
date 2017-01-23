CREATE TABLE [dbo].[PersonPoliticalTitle] (
    [ID]                INT            IDENTITY (1, 1) NOT NULL,
    [Abbreviation]      NVARCHAR (10)  NULL,
    [Name]              NVARCHAR (50)  NULL,
    [Description]       NVARCHAR (255) NULL,
    [PersonTitleTypeID] INT            NULL,
    CONSTRAINT [PK_PersonPoliticalTitle] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonPoliticalTitle_PersonTitleType] FOREIGN KEY ([PersonTitleTypeID]) REFERENCES [dbo].[PersonTitleType] ([ID])
);

