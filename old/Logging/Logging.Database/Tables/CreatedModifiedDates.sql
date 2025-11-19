CREATE TABLE [dbo].[CreatedModifiedDates] (
    [ID]               INT      IDENTITY (1, 1) NOT NULL,
    [CreateDate]       DATETIME NOT NULL,
    [LastModifiedDate] DATETIME NOT NULL,
    CONSTRAINT [PK_CreatedModifiedDates] PRIMARY KEY CLUSTERED ([ID] ASC)
);

