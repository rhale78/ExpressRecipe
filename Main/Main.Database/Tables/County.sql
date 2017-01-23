CREATE TABLE [dbo].[County] (
    [ID]   INT           IDENTITY (1, 1) NOT NULL,
    [Name] NVARCHAR (50) NULL,
    CONSTRAINT [PK_County] PRIMARY KEY CLUSTERED ([ID] ASC)
);

