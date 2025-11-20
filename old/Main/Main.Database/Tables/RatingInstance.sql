CREATE TABLE [dbo].[RatingInstance] (
    [ID]    INT           IDENTITY (1, 1) NOT NULL,
    [Name]  NVARCHAR (50) NULL,
    [Value] INT           NULL,
    CONSTRAINT [PK_RatingInstance] PRIMARY KEY CLUSTERED ([ID] ASC)
);

