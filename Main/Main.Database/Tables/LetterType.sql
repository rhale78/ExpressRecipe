CREATE TABLE [dbo].[LetterType] (
    [ID]          INT        IDENTITY (1, 1) NOT NULL,
    [Name]        NCHAR (10) NULL,
    [Description] NCHAR (10) NULL,
    CONSTRAINT [PK_LetterType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Casual, formal, friendly, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'LetterType', @level2type = N'COLUMN', @level2name = N'Name';

