CREATE TABLE [dbo].[User] (
    [ID]                   INT IDENTITY (1, 1) NOT NULL,
    [UseDatesID]           INT NULL,
    [CreatedUpdatedDateID] INT NULL,
    [PersonID]             INT NULL,
    CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_User_CreatedUpdatedDate] FOREIGN KEY ([CreatedUpdatedDateID]) REFERENCES [dbo].[CreatedUpdatedDate] ([ID]),
    CONSTRAINT [FK_User_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID]),
    CONSTRAINT [FK_User_UseDates] FOREIGN KEY ([UseDatesID]) REFERENCES [dbo].[UseDates] ([ID])
);

