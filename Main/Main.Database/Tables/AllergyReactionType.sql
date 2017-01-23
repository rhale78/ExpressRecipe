CREATE TABLE [dbo].[AllergyReactionType] (
    [ID]          INT            IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (25)  NULL,
    [Description] NVARCHAR (255) NULL,
    CONSTRAINT [PK_AllergyReactionType] PRIMARY KEY CLUSTERED ([ID] ASC)
);


GO
EXECUTE sp_addextendedproperty @name = N'MS_Description', @value = N'Rash, asthma, vomiting, diarrhea, swealing, etc', @level0type = N'SCHEMA', @level0name = N'dbo', @level1type = N'TABLE', @level1name = N'AllergyReactionType', @level2type = N'COLUMN', @level2name = N'Name';

