CREATE TABLE [dbo].[AllergyProximityType] (
    [ID]          INT            IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (25)  NULL,
    [Description] NVARCHAR (255) NULL,
    CONSTRAINT [PK_AllergyProximityType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Contained in, manufactured in/near, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'AllergyProximityType', @level2type = N'COLUMN', @level2name = N'Name';

