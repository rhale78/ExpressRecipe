CREATE TABLE [dbo].[PersonRelationship] (
    [ID]                       INT IDENTITY (1, 1) NOT NULL,
    [PersonRelationshipTypeID] INT NULL,
    [PrimaryPersonID]          INT NULL,
    [SecondaryPersonID]        INT NULL,
    CONSTRAINT [PK_PersonRelationship] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonRelationship_Person] FOREIGN KEY ([PrimaryPersonID]) REFERENCES [dbo].[Person] ([ID]),
    CONSTRAINT [FK_PersonRelationship_Person1] FOREIGN KEY ([SecondaryPersonID]) REFERENCES [dbo].[Person] ([ID]),
    CONSTRAINT [FK_PersonRelationship_PersonRelationshipType] FOREIGN KEY ([PersonRelationshipTypeID]) REFERENCES [dbo].[PersonRelationshipType] ([ID])
);

