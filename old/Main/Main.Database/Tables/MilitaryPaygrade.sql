CREATE TABLE [dbo].[MilitaryPaygrade] (
    [ID]                     INT        IDENTITY (1, 1) NOT NULL,
    [MilitaryPaygradeTypeID] INT        NULL,
    [PaygradeIndex]          NCHAR (10) NULL,
    CONSTRAINT [PK_MilitaryPaygrade] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_MilitaryPaygrade_MilitaryPaygradeType] FOREIGN KEY ([MilitaryPaygradeTypeID]) REFERENCES [dbo].[MilitaryPaygradeType] ([ID])
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'1, 2, 3, etc for E1, E2, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'MilitaryPaygrade', @level2type = N'COLUMN', @level2name = N'PaygradeIndex';

