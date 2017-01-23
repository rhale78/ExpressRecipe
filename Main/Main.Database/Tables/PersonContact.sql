CREATE TABLE [dbo].[PersonContact] (
    [ID]           INT           IDENTITY (1, 1) NOT NULL,
    [FirstName]    NVARCHAR (75) NULL,
    [LastName]     NVARCHAR (75) NULL,
    [EmailAddress] NVARCHAR (75) NULL,
    [Address]      NVARCHAR (75) NULL,
    [City]         NVARCHAR (75) NULL,
    [State]        NVARCHAR (75) NULL,
    [Zip]          NVARCHAR (75) NULL,
    [PhoneNumber]  NVARCHAR (75) NULL,
    [Relationship] NVARCHAR (75) NULL,
    [PersonID]     INT           NULL,
    [SourceTypeID] INT           NULL,
    CONSTRAINT [PK_PersonContact] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonContact_ContactSourceType] FOREIGN KEY ([SourceTypeID]) REFERENCES [dbo].[ContactSourceType] ([ID]),
    CONSTRAINT [FK_PersonContact_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID])
);

