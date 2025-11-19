CREATE TABLE [dbo].[AddressType] (
    [ID]          INT           IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (25) NULL,
    [Description] NVARCHAR (50) NULL,
    CONSTRAINT [PK_AddressType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Business, home, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'AddressType', @level2type = N'COLUMN', @level2name = N'Name';

