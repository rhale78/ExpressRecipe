CREATE TABLE [dbo].[PersonMarriageEndType] (
    [ID]          INT        IDENTITY (1, 1) NOT NULL,
    [Name]        NCHAR (10) NULL,
    [Description] NCHAR (10) NULL,
    CONSTRAINT [PK_PersonMarriageEndType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'N/A, divorce, death, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PersonMarriageEndType', @level2type = N'COLUMN', @level2name = N'Name';

