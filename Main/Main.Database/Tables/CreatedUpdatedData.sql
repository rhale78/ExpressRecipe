CREATE TABLE [dbo].[CreatedUpdatedData] (
    [ID]                   INT IDENTITY (1, 1) NOT NULL,
    [CreatedByUserID]      INT NULL,
    [LastModifiedByUserID] INT NULL,
    [CreatedUpdatedDateID] INT NULL,
    CONSTRAINT [PK_CreatedUpdatedData] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_CreatedUpdatedData_CreatedUpdatedDate] FOREIGN KEY ([CreatedUpdatedDateID]) REFERENCES [dbo].[CreatedUpdatedDate] ([ID]),
    CONSTRAINT [FK_CreatedUpdatedData_User] FOREIGN KEY ([CreatedByUserID]) REFERENCES [dbo].[User] ([ID]),
    CONSTRAINT [FK_CreatedUpdatedData_User1] FOREIGN KEY ([LastModifiedByUserID]) REFERENCES [dbo].[User] ([ID])
);

