CREATE TABLE [dbo].[MedicalCondition] (
    [ID]          INT            IDENTITY (1, 1) NOT NULL,
    [Name]        NVARCHAR (50)  NULL,
    [Description] NVARCHAR (255) NULL,
    CONSTRAINT [PK_MedicalCondition] PRIMARY KEY CLUSTERED ([ID] ASC)
);

