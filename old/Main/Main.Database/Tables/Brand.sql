CREATE TABLE [dbo].[Brand] (
    [ID]   INT           IDENTITY (1, 1) NOT NULL,
    [Name] NVARCHAR (50) NULL,
    CONSTRAINT [PK_Brand] PRIMARY KEY CLUSTERED ([ID] ASC)
);

