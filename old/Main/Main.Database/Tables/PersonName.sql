CREATE TABLE [dbo].[PersonName] (
    [ID]                 INT           IDENTITY (1, 1) NOT NULL,
    [PersonNamePrefixID] INT           NULL,
    [LegalFirstName]     NVARCHAR (50) NULL,
    [PreferredFirstName] NVARCHAR (50) NULL,
    [MiddleName]         NVARCHAR (50) NULL,
    [MaidenName]         NVARCHAR (50) NULL,
    [LastName]           NVARCHAR (50) NULL,
    [PersonNameSuffixID] INT           NULL,
    [PersonNameDegreeID] INT           NULL,
    CONSTRAINT [PK_PersonName] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonName_PersonNameDegree] FOREIGN KEY ([PersonNameDegreeID]) REFERENCES [dbo].[PersonNameDegree] ([ID]),
    CONSTRAINT [FK_PersonName_PersonNamePrefix] FOREIGN KEY ([PersonNamePrefixID]) REFERENCES [dbo].[PersonNamePrefix] ([ID]),
    CONSTRAINT [FK_PersonName_PersonNameSuffix] FOREIGN KEY ([PersonNameSuffixID]) REFERENCES [dbo].[PersonNameSuffix] ([ID])
);

