CREATE TABLE [dbo].[PersonMarriage] (
    [ID]                     INT IDENTITY (1, 1) NOT NULL,
    [Person1ID]              INT NULL,
    [Person2ID]              INT NULL,
    [PersonMaritalStatusID]  INT NULL,
    [WeddingImportantDateID] INT NULL,
    [EndImportantDateID]     INT NULL,
    [MarriageEndTypeID]      INT NULL,
    CONSTRAINT [PK_PersonMarriage] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonMarriage_Person] FOREIGN KEY ([Person1ID]) REFERENCES [dbo].[Person] ([ID]),
    CONSTRAINT [FK_PersonMarriage_Person1] FOREIGN KEY ([Person2ID]) REFERENCES [dbo].[Person] ([ID]),
    CONSTRAINT [FK_PersonMarriage_PersonImportantDate] FOREIGN KEY ([EndImportantDateID]) REFERENCES [dbo].[PersonImportantDate] ([ID]),
    CONSTRAINT [FK_PersonMarriage_PersonImportantDate1] FOREIGN KEY ([WeddingImportantDateID]) REFERENCES [dbo].[PersonImportantDate] ([ID]),
    CONSTRAINT [FK_PersonMarriage_PersonMaritalStatus] FOREIGN KEY ([PersonMaritalStatusID]) REFERENCES [dbo].[PersonMaritalStatus] ([ID]),
    CONSTRAINT [FK_PersonMarriage_PersonMarriageEndType] FOREIGN KEY ([MarriageEndTypeID]) REFERENCES [dbo].[PersonMarriageEndType] ([ID])
);

