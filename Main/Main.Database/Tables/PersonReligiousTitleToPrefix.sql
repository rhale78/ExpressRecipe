CREATE TABLE [dbo].[PersonReligiousTitleToPrefix] (
    [ID]                     INT IDENTITY (1, 1) NOT NULL,
    [PersonReligiousTitleID] INT NULL,
    [PersonNamePrefixID]     INT NULL,
    CONSTRAINT [PK_PersonReligiousTitleToPrefix] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonReligiousTitleToPrefix_PersonNamePrefix] FOREIGN KEY ([PersonNamePrefixID]) REFERENCES [dbo].[PersonNamePrefix] ([ID]),
    CONSTRAINT [FK_PersonReligiousTitleToPrefix_PersonReligiousTitle] FOREIGN KEY ([PersonReligiousTitleID]) REFERENCES [dbo].[PersonReligiousTitle] ([ID])
);

