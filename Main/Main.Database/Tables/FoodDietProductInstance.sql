CREATE TABLE [dbo].[FoodDietProductInstance] (
    [ID]                INT IDENTITY (1, 1) NOT NULL,
    [FoodDietID]        INT NULL,
    [ProductInstanceID] INT NULL,
    [ConsumeTypeID]     INT NULL,
    CONSTRAINT [PK_FoodDietProductInstance] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_FoodDietProductInstance_FoodConsumeType] FOREIGN KEY ([ConsumeTypeID]) REFERENCES [dbo].[FoodConsumeType] ([ID]),
    CONSTRAINT [FK_FoodDietProductInstance_FoodDiet] FOREIGN KEY ([FoodDietID]) REFERENCES [dbo].[FoodDiet] ([ID]),
    CONSTRAINT [FK_FoodDietProductInstance_ProductInstance] FOREIGN KEY ([ProductInstanceID]) REFERENCES [dbo].[ProductInstance] ([ID])
);

