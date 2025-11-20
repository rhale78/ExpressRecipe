CREATE TABLE [dbo].[PersonEmailAddress] (
    [ID]             INT IDENTITY (1, 1) NOT NULL,
    [PersonID]       INT NULL,
    [EmailAddressID] INT NULL,
    CONSTRAINT [PK_PersonEmailAddress] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonEmailAddress_EmailAddress] FOREIGN KEY ([EmailAddressID]) REFERENCES [dbo].[EmailAddress] ([ID]),
    CONSTRAINT [FK_PersonEmailAddress_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID])
);

