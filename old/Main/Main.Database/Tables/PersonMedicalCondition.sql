CREATE TABLE [dbo].[PersonMedicalCondition] (
    [ID]                 INT IDENTITY (1, 1) NOT NULL,
    [PersonID]           INT NULL,
    [MedicalConditionID] INT NULL,
    CONSTRAINT [PK_PersonMedicalCondition] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonMedicalCondition_MedicalCondition] FOREIGN KEY ([MedicalConditionID]) REFERENCES [dbo].[MedicalCondition] ([ID]),
    CONSTRAINT [FK_PersonMedicalCondition_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID])
);

