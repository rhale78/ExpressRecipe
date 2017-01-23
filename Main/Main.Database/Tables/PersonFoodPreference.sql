CREATE TABLE [dbo].[PersonFoodPreference] (
    [ID]               INT IDENTITY (1, 1) NOT NULL,
    [PersonID]         INT NULL,
    [FoodPreferenceID] INT NULL,
    [RatingInstanceID] INT NULL,
    CONSTRAINT [PK_PersonFoodPreference] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonFoodPreference_FoodPreference] FOREIGN KEY ([FoodPreferenceID]) REFERENCES [dbo].[FoodPreference] ([ID]),
    CONSTRAINT [FK_PersonFoodPreference_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID]),
    CONSTRAINT [FK_PersonFoodPreference_RatingInstance] FOREIGN KEY ([RatingInstanceID]) REFERENCES [dbo].[RatingInstance] ([ID])
);

