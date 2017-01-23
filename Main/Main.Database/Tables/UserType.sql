CREATE TABLE [dbo].[UserType] (
    [ID]               INT IDENTITY (1, 1) NOT NULL,
    [UserID]           INT NULL,
    [UseDatesID]       INT NULL,
    [UserAccessTypeID] INT NULL,
    CONSTRAINT [PK_UserType] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_UserType_UseDates] FOREIGN KEY ([UseDatesID]) REFERENCES [dbo].[UseDates] ([ID]),
    CONSTRAINT [FK_UserType_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([ID]),
    CONSTRAINT [FK_UserType_UserAccessType] FOREIGN KEY ([UserAccessTypeID]) REFERENCES [dbo].[UserAccessType] ([ID])
);

