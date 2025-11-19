CREATE TABLE [dbo].[PersonImportantDateType] (
    [ID]          INT           IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (50) NULL,
    [Description] NVARCHAR (50) NULL,
    CONSTRAINT [PK_PersonImportantDateType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Birthdate, anniversary, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'PersonImportantDateType', @level2type = N'COLUMN', @level2name = N'Name';

