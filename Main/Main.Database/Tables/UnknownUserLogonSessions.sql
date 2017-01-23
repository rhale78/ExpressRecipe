CREATE TABLE [dbo].[UnknownUserLogonSessions] (
    [ID]                          INT           IDENTITY (1, 1) NOT NULL,
    [SourceIPAddress]             NVARCHAR (50) NULL,
    [Username]                    NVARCHAR (50) NULL,
    [Password]                    NVARCHAR (50) NULL,
    [UserRegisterredAfterFailure] BIT           NULL,
    [UserLoginID]                 INT           NULL,
    [UseDatesID]                  INT           NULL,
    [NumberInvalidAttemptsThisIP] INT           NULL,
    CONSTRAINT [PK_UnknownUserLogonSessions] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_UnknownUserLogonSessions_UseDates] FOREIGN KEY ([UseDatesID]) REFERENCES [dbo].[UseDates] ([ID]),
    CONSTRAINT [FK_UnknownUserLogonSessions_UserLogon] FOREIGN KEY ([UserLoginID]) REFERENCES [dbo].[UserLogon] ([ID])
);

