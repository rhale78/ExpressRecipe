CREATE TABLE [dbo].[PersonReligionToReligionTitle] (
    [ID]                     INT IDENTITY (1, 1) NOT NULL,
    [PersonReligionID]       INT NULL,
    [PersonReligiousTitleID] INT NULL,
    CONSTRAINT [PK_PersonReligionToReligionTitle] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonReligionToReligionTitle_PersonReligion] FOREIGN KEY ([PersonReligionID]) REFERENCES [dbo].[PersonReligion] ([ID]),
    CONSTRAINT [FK_PersonReligionToReligionTitle_PersonReligiousTitle] FOREIGN KEY ([PersonReligiousTitleID]) REFERENCES [dbo].[PersonReligiousTitle] ([ID])
);

