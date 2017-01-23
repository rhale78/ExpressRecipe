CREATE TABLE [dbo].[FoodAllergyClassification] (
    [ID]          INT            IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (25)  NULL,
    [Description] NVARCHAR (255) NULL,
    CONSTRAINT [PK_FoodAllergyClassification] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Shellfish, nuts, berry, citrus, soy, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'FoodAllergyClassification', @level2type = N'COLUMN', @level2name = N'Name';

