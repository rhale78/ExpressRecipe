CREATE TABLE [dbo].[Street] (
    [ID]   INT           IDENTITY (1, 1) NOT NULL,
    [Name] NVARCHAR (75) NULL,
    CONSTRAINT [PK_Street] PRIMARY KEY CLUSTERED ([ID] ASC)
);

