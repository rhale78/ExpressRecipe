CREATE TABLE [dbo].[PersonProductAllergy] (
    [ID]                         INT IDENTITY (1, 1) NOT NULL,
    [PersonID]                   INT NULL,
    [ProductID]                  INT NULL,
    [AllergySeverityProxReactID] INT NULL,
    CONSTRAINT [PK_PersonProductAllergy] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonProductAllergy_AllergySeverityProxReact] FOREIGN KEY ([AllergySeverityProxReactID]) REFERENCES [dbo].[AllergySeverityProxReact] ([ID]),
    CONSTRAINT [FK_PersonProductAllergy_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID]),
    CONSTRAINT [FK_PersonProductAllergy_Product] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[Product] ([ID])
);

