CREATE TABLE [dbo].[AddressUse] (
    [ID]          INT           IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (25) NULL,
    [Description] NVARCHAR (50) NULL,
    CONSTRAINT [PK_AddressUse] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Physical, mailing', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'AddressUse', @level2type = N'COLUMN', @level2name = N'Name';

