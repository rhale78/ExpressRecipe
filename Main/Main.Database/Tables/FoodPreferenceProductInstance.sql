CREATE TABLE [dbo].[FoodPreferenceProductInstance] (
    [ID]                INT IDENTITY (1, 1) NOT NULL,
    [FoodPreferenceID]  INT NULL,
    [ProductInstanceID] INT NULL,
    CONSTRAINT [PK_FoodPreferenceProductInstance] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_FoodPreferenceProductInstance_FoodPreference] FOREIGN KEY ([FoodPreferenceID]) REFERENCES [dbo].[FoodPreference] ([ID]),
    CONSTRAINT [FK_FoodPreferenceProductInstance_ProductInstance] FOREIGN KEY ([ProductInstanceID]) REFERENCES [dbo].[ProductInstance] ([ID])
);

