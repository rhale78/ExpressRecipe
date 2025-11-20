CREATE TABLE [dbo].[PersonProductInstanceAllergy] (
    [ID]                         INT IDENTITY (1, 1) NOT NULL,
    [PersonID]                   INT NULL,
    [ProductInstanceID]          INT NULL,
    [AllergySeverityProxReactID] INT NULL,
    CONSTRAINT [PK_PersonProductInstanceAllergy] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonProductInstanceAllergy_AllergySeverityProxReact] FOREIGN KEY ([AllergySeverityProxReactID]) REFERENCES [dbo].[AllergySeverityProxReact] ([ID]),
    CONSTRAINT [FK_PersonProductInstanceAllergy_ProductInstance] FOREIGN KEY ([ProductInstanceID]) REFERENCES [dbo].[ProductInstance] ([ID])
);

