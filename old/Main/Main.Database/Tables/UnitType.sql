CREATE TABLE [dbo].[UnitType] (
    [ID]          INT            IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (25)  NULL,
    [Description] NVARCHAR (255) NULL,
    [IsMetric]    BIT            NULL,
    CONSTRAINT [PK_UnitType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Weight, volume, time, size, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'UnitType', @level2type = N'COLUMN', @level2name = N'Name';

