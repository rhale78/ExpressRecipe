CREATE TABLE [dbo].[RelationshipGenderRestriction] (
    [ID]                       INT IDENTITY (1, 1) NOT NULL,
    [PersonGenderID]           INT NULL,
    [PersonRelationshipTypeID] INT NULL,
    CONSTRAINT [PK_RelationshipGenderRestriction] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_RelationshipGenderRestriction_PersonGender] FOREIGN KEY ([PersonGenderID]) REFERENCES [dbo].[PersonGender] ([ID]),
    CONSTRAINT [FK_RelationshipGenderRestriction_PersonRelationshipType] FOREIGN KEY ([PersonRelationshipTypeID]) REFERENCES [dbo].[PersonRelationshipType] ([ID])
);

