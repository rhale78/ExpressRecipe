CREATE TABLE [dbo].[FoodConsumeType] (
    [ID]          INT            IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (50)  NULL,
    [Description] NVARCHAR (255) NULL,
    CONSTRAINT [PK_FoodConsumeType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Avoid, encourage, limit, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'FoodConsumeType', @level2type = N'COLUMN', @level2name = N'Name';

