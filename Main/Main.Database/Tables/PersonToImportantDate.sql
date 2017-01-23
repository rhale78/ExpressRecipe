CREATE TABLE [dbo].[PersonToImportantDate] (
    [ID]                    INT IDENTITY (1, 1) NOT NULL,
    [PersonID]              INT NULL,
    [PersonImportantDateID] INT NULL,
    CONSTRAINT [PK_PersonToImportantDate] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonToImportantDate_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID]),
    CONSTRAINT [FK_PersonToImportantDate_PersonImportantDate] FOREIGN KEY ([PersonImportantDateID]) REFERENCES [dbo].[PersonImportantDate] ([ID])
);

