CREATE TABLE [dbo].[FoodRestrictionProductInstance] (
    [ID]                INT IDENTITY (1, 1) NOT NULL,
    [FoodRestrictionID] INT NULL,
    [ProductInstanceID] INT NULL,
    CONSTRAINT [PK_FoodRestrictionProductInstance] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_FoodRestrictionProductInstance_FoodRestriction] FOREIGN KEY ([FoodRestrictionID]) REFERENCES [dbo].[FoodRestriction] ([ID]),
    CONSTRAINT [FK_FoodRestrictionProductInstance_ProductInstance] FOREIGN KEY ([ProductInstanceID]) REFERENCES [dbo].[ProductInstance] ([ID])
);

