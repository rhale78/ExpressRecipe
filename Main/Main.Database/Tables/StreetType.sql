CREATE TABLE [dbo].[StreetType] (
    [ID]           INT           IDENTITY (1, 1) NOT NULL,
    [Abbreviation] NVARCHAR (25) NULL,
    [Name]         NVARCHAR (50) NULL,
    [Description]  NVARCHAR (50) NULL,
    CONSTRAINT [PK_StreetType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Circle, Steet, Road, Highway, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'StreetType', @level2type = N'COLUMN', @level2name = N'Name';

