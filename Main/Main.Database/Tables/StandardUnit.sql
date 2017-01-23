CREATE TABLE [dbo].[StandardUnit] (
    [ID]           INT            IDENTITY (1, 1) NOT NULL,
    [Name]         NVARCHAR (25)  NULL,
    [Description]  NVARCHAR (255) NULL,
    [Abbreviation] NVARCHAR (25)  NULL,
    [UnitTypeID]   INT            NULL,
    CONSTRAINT [PK_StandardUnit] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_StandardUnit_UnitType] FOREIGN KEY ([UnitTypeID]) REFERENCES [dbo].[UnitType] ([ID])
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Inches, feet, seconds, ounces, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StandardUnit', @level2type = N'COLUMN', @level2name = N'Name';

