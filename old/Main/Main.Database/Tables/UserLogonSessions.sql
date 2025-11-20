CREATE TABLE [dbo].[UserLogonSessions] (
    [ID]                          INT           IDENTITY (1, 1) NOT NULL,
    [UserLogonID]                 INT           NULL,
    [Successful]                  BIT           NULL,
    [NumberInvalidAttemptsThisIP] INT           NULL,
    [LogoutTypeID]                INT           NULL,
    [SourceIPAddress]             NVARCHAR (50) NULL,
    [Username]                    NVARCHAR (50) NULL,
    [Password]                    NVARCHAR (50) NULL,
    [UseDatesID]                  INT           NULL,
    CONSTRAINT [PK_UserLogonSessions] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_UserLogonSessions_LogoutType] FOREIGN KEY ([LogoutTypeID]) REFERENCES [dbo].[LogoutType] ([ID]),
    CONSTRAINT [FK_UserLogonSessions_UseDates] FOREIGN KEY ([UseDatesID]) REFERENCES [dbo].[UseDates] ([ID]),
    CONSTRAINT [FK_UserLogonSessions_UserLogon] FOREIGN KEY ([UserLogonID]) REFERENCES [dbo].[UserLogon] ([ID])
);

