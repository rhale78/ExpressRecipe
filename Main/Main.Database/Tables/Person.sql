CREATE TABLE [dbo].[Person] (
    [ID]                    INT IDENTITY (1, 1) NOT NULL,
    [PersonNameID]          INT NULL,
    [PersonGenderID]        INT NULL,
    [PersonMaritalStatusID] INT NULL,
    [BirthImportantDateID]  INT NULL,
    CONSTRAINT [PK_Person] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_Person_PersonGender] FOREIGN KEY ([PersonGenderID]) REFERENCES [dbo].[PersonGender] ([ID]),
    CONSTRAINT [FK_Person_PersonImportantDate] FOREIGN KEY ([BirthImportantDateID]) REFERENCES [dbo].[PersonImportantDate] ([ID]),
    CONSTRAINT [FK_Person_PersonMaritalStatus] FOREIGN KEY ([PersonMaritalStatusID]) REFERENCES [dbo].[PersonMaritalStatus] ([ID]),
    CONSTRAINT [FK_Person_PersonName] FOREIGN KEY ([PersonNameID]) REFERENCES [dbo].[PersonName] ([ID]),
    CONSTRAINT [FK_Person_PersonProductInstanceAllergy] FOREIGN KEY ([ID]) REFERENCES [dbo].[PersonProductInstanceAllergy] ([ID])
);

