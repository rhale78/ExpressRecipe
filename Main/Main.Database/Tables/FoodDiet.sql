CREATE TABLE [dbo].[FoodDiet] (
    [ID]             INT            IDENTITY (1, 1) NOT NULL,
    [FoodDietTypeID] INT            NULL,
    [Name]           NVARCHAR (50)  NULL,
    [Description]    NVARCHAR (255) NULL,
    CONSTRAINT [PK_FoodDiet] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_FoodDiet_FoodDietType] FOREIGN KEY ([FoodDietTypeID]) REFERENCES [dbo].[FoodDietType] ([ID])
);

