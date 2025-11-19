CREATE TABLE [dbo].[FoodRestriction] (
    [ID]                    INT            IDENTITY (1, 1) NOT NULL,
    [Name]                  NVARCHAR (50)  NULL,
    [Description]           NVARCHAR (255) NULL,
    [FoodRestrictionTypeID] INT            NULL,
    CONSTRAINT [PK_FoodRestriction] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_FoodRestriction_FoodRestrictionType] FOREIGN KEY ([FoodRestrictionTypeID]) REFERENCES [dbo].[FoodRestrictionType] ([ID])
);

