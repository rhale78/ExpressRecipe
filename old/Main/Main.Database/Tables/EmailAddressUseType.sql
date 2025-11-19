CREATE TABLE [dbo].[EmailAddressUseType] (
    [ID]          INT           IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (50) NULL,
    [Description] NVARCHAR (75) NULL,
    CONSTRAINT [PK_EmailAddressUseType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Home/personal, business', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'EmailAddressUseType', @level2type = N'COLUMN', @level2name = N'Name';

