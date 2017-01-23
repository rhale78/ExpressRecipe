CREATE TABLE [dbo].[PersonTitle] (
    [ID]                INT IDENTITY (1, 1) NOT NULL,
    [PersonTitleTypeID] INT NULL,
    [PersonNameID]      INT NULL,
    CONSTRAINT [PK_PersonTitle] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonTitle_PersonName] FOREIGN KEY ([PersonNameID]) REFERENCES [dbo].[PersonName] ([ID]),
    CONSTRAINT [FK_PersonTitle_PersonTitleType] FOREIGN KEY ([PersonTitleTypeID]) REFERENCES [dbo].[PersonTitleType] ([ID])
);

