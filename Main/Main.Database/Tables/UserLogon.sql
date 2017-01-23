CREATE TABLE [dbo].[UserLogon] (
    [ID]                         INT           IDENTITY (1, 1) NOT NULL,
    [UserID]                     INT           NULL,
    [Username]                   NVARCHAR (50) NULL,
    [Password]                   NVARCHAR (50) NULL,
    [UseDatesID]                 INT           NULL,
    [LastModifiedDate]           DATE          NULL,
    [NumberTotalInvalidAttempts] INT           NULL,
    [NeedsPasswordReset]         BIT           NULL,
    [NeedsVerification]          BIT           NULL,
    CONSTRAINT [PK_UserLogon] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_UserLogon_UseDates] FOREIGN KEY ([UseDatesID]) REFERENCES [dbo].[UseDates] ([ID]),
    CONSTRAINT [FK_UserLogon_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([ID])
);

