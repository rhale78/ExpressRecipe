CREATE TABLE [dbo].[StandardUnitConversion] (
    [ID]                 INT            IDENTITY (1, 1) NOT NULL,
    [Name]               NVARCHAR (25)  NULL,
    [Description]        NVARCHAR (255) NULL,
    [Amount]             FLOAT (53)     NULL,
    [FromStandardUnitID] INT            NULL,
    [ToStandardUnitIT]   INT            NULL,
    [OffsetAmount]       FLOAT (53)     NULL,
    CONSTRAINT [PK_StandardUnitConversion] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_StandardUnitConversion_StandardUnit] FOREIGN KEY ([FromStandardUnitID]) REFERENCES [dbo].[StandardUnit] ([ID]),
    CONSTRAINT [FK_StandardUnitConversion_StandardUnit1] FOREIGN KEY ([ToStandardUnitIT]) REFERENCES [dbo].[StandardUnit] ([ID])
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Inches to feet, oz to pounds, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StandardUnitConversion', @level2type = N'COLUMN', @level2name = N'Name';

