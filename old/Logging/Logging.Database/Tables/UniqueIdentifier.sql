CREATE TABLE [dbo].[UniqueIdentifier] (
    [ID]                     INT            IDENTITY (1, 1) NOT NULL,
    [UniqueIdentifierTypeID] INT            NOT NULL,
    [Identifier]             NVARCHAR (255) NOT NULL,
    CONSTRAINT [PK_UniqueIdentifier] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_UniqueIdentifier_UniqueIdentifierType] FOREIGN KEY ([UniqueIdentifierTypeID]) REFERENCES [dbo].[UniqueIdentifierType] ([ID])
);

