CREATE TABLE [dbo].[PersonMaritalStatus] (
    [ID]           INT            IDENTITY (1, 1) NOT NULL,
    [Abbreviation] NVARCHAR (10)  NULL,
    [Name]         NVARCHAR (50)  NULL,
    [Description]  NVARCHAR (255) NULL,
    CONSTRAINT [PK_PersonMaritalStatus] PRIMARY KEY CLUSTERED ([ID] ASC)
);

