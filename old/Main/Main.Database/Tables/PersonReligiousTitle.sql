CREATE TABLE [dbo].[PersonReligiousTitle] (
    [ID]                INT        IDENTITY (1, 1) NOT NULL,
    [Title]             NCHAR (10) NULL,
    [PersonTitleTypeID] INT        NULL,
    CONSTRAINT [PK_PersonReligiousTitle] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonReligiousTitle_PersonTitleType] FOREIGN KEY ([PersonTitleTypeID]) REFERENCES [dbo].[PersonTitleType] ([ID])
);

