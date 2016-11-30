CREATE TABLE [dbo].[UseDates] (
    [ID]           INT      IDENTITY (1, 1) NOT NULL,
    [FirstUseDate] DATETIME NOT NULL,
    [LastUseDate]  DATETIME NOT NULL,
    CONSTRAINT [PK_UseDates] PRIMARY KEY CLUSTERED ([ID] ASC)
);

