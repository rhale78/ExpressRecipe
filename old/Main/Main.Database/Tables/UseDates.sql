CREATE TABLE [dbo].[UseDates] (
    [ID]           INT  IDENTITY (1, 1) NOT NULL,
    [FirstUseDate] DATE NULL,
    [LastUseDate]  DATE NULL,
    CONSTRAINT [PK_UseDates] PRIMARY KEY CLUSTERED ([ID] ASC)
);

