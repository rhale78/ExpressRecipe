CREATE TABLE [dbo].[UserAccountStatus] (
    [ID]                      INT IDENTITY (1, 1) NOT NULL,
    [UserAccountStatusTypeID] INT NULL,
    [UseDatesID]              INT NULL,
    [UserID]                  INT NULL,
    CONSTRAINT [PK_UserAccountStatus] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_UserAccountStatus_UseDates] FOREIGN KEY ([UseDatesID]) REFERENCES [dbo].[UseDates] ([ID]),
    CONSTRAINT [FK_UserAccountStatus_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([ID]),
    CONSTRAINT [FK_UserAccountStatus_UserAccessPermissionItem] FOREIGN KEY ([UserAccountStatusTypeID]) REFERENCES [dbo].[UserAccessPermissionItem] ([ID]),
    CONSTRAINT [FK_UserAccountStatus_UserAccountStatusType] FOREIGN KEY ([UserAccountStatusTypeID]) REFERENCES [dbo].[UserAccountStatusType] ([ID])
);

