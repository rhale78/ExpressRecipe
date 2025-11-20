CREATE TABLE [dbo].[MilitaryPaygradeType] (
    [ID]             INT        IDENTITY (1, 1) NOT NULL,
    [Name]           NCHAR (10) NULL,
    [Description]    NCHAR (10) NULL,
    [PaygradePrefix] NCHAR (10) NULL,
    CONSTRAINT [PK_MilitaryPaygradeType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Enlisted, officer, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'MilitaryPaygradeType', @level2type = N'COLUMN', @level2name = N'Name';


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'E, O, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'MilitaryPaygradeType', @level2type = N'COLUMN', @level2name = N'PaygradePrefix';

