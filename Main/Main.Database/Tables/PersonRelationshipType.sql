CREATE TABLE [dbo].[PersonRelationshipType] (
    [ID]                        INT           IDENTITY (1, 1) NOT NULL,
    [Name]                      NVARCHAR (50) NULL,
    [Description]               NVARCHAR (75) NULL,
    [IsBloodRelation]           BIT           NULL,
    [InverseRelationshipTypeID] INT           NULL,
    CONSTRAINT [PK_PersonRelationshipType] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonRelationshipType_PersonRelationshipType] FOREIGN KEY ([InverseRelationshipTypeID]) REFERENCES [dbo].[PersonRelationshipType] ([ID])
);

