CREATE TABLE [dbo].[ContactSourceType] (
    [ID]          INT           IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (50) NULL,
    [Description] NVARCHAR (50) NULL,
    CONSTRAINT [PK_ContactSourceType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Google contacts, manual entry, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'ContactSourceType', @level2type = N'COLUMN', @level2name = N'Name';

