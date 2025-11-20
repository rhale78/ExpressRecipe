CREATE TABLE [dbo].[MilitaryTitlePaygradeType] (
    [ID]                     INT IDENTITY (1, 1) NOT NULL,
    [PersonPrefixNameID]     INT NULL,
    [MilitaryPaygradeTypeID] INT NULL,
    CONSTRAINT [PK_MilitaryTitlePaygradeType] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_MilitaryTitlePaygradeType_MilitaryPaygradeType] FOREIGN KEY ([MilitaryPaygradeTypeID]) REFERENCES [dbo].[MilitaryPaygradeType] ([ID]),
    CONSTRAINT [FK_MilitaryTitlePaygradeType_PersonNamePrefix] FOREIGN KEY ([PersonPrefixNameID]) REFERENCES [dbo].[PersonNamePrefix] ([ID])
);

