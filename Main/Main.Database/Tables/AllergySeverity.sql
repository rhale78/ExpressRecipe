CREATE TABLE [dbo].[AllergySeverity] (
    [ID]                 INT            IDENTITY (1, 1) NOT NULL,
    [Name]               NVARCHAR (25)  NULL,
    [Description]        NVARCHAR (255) NULL,
    [SeverityLevelIndex] INT            NULL,
    CONSTRAINT [PK_AllergySeverity] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Mild, moderate, high, extreme, death', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'AllergySeverity', @level2type = N'COLUMN', @level2name = N'Name';

