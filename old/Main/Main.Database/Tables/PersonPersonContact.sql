CREATE TABLE [dbo].[PersonPersonContact] (
    [ID]              INT IDENTITY (1, 1) NOT NULL,
    [PersonID]        INT NULL,
    [PersonContactID] INT NULL,
    CONSTRAINT [PK_PersonPersonContact] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonPersonContact_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID]),
    CONSTRAINT [FK_PersonPersonContact_PersonContact] FOREIGN KEY ([PersonContactID]) REFERENCES [dbo].[PersonContact] ([ID])
);

