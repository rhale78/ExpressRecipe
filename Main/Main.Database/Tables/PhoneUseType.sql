CREATE TABLE [dbo].[PhoneUseType] (
    [ID]           INT           IDENTITY (1, 1) NOT NULL,
    [Name]         NVARCHAR (10) NULL,
    [Abbreviation] NVARCHAR (10) NULL,
    [Description]  NVARCHAR (50) NULL,
    CONSTRAINT [PK_PhoneUseType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Home, business, alternate, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PhoneUseType', @level2type = N'COLUMN', @level2name = N'Name';

