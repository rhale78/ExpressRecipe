CREATE TABLE [dbo].[SalutationLetterTypeTitle] (
    [ID]                 INT IDENTITY (1, 1) NOT NULL,
    [PersonSalutationID] INT NULL,
    [LetterTypeID]       INT NULL,
    [PersonTitleID]      INT NULL,
    CONSTRAINT [PK_SalutationLetterTypeTitle] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_SalutationLetterTypeTitle_LetterType] FOREIGN KEY ([LetterTypeID]) REFERENCES [dbo].[LetterType] ([ID]),
    CONSTRAINT [FK_SalutationLetterTypeTitle_PersonSalutation] FOREIGN KEY ([PersonSalutationID]) REFERENCES [dbo].[PersonSalutation] ([ID]),
    CONSTRAINT [FK_SalutationLetterTypeTitle_PersonTitle] FOREIGN KEY ([PersonTitleID]) REFERENCES [dbo].[PersonTitle] ([ID])
);

