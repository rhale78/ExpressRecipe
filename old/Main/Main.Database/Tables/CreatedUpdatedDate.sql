CREATE TABLE [dbo].[CreatedUpdatedDate] (
    [ID]                   INT      IDENTITY (1, 1) NOT NULL,
    [CreatedDateTime]      DATETIME NULL,
    [LastModifiedDateTime] DATETIME NULL,
    CONSTRAINT [PK_CreatedUpdatedDate] PRIMARY KEY CLUSTERED ([ID] ASC)
);

