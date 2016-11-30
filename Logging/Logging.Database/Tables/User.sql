CREATE TABLE [dbo].[User] (
    [ID]               INT      IDENTITY (1, 1) NOT NULL,
    [UseDatesID]       INT      NOT NULL,
    [LastModifiedDate] DATETIME NOT NULL,
    CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_User_UseDates] FOREIGN KEY ([UseDatesID]) REFERENCES [dbo].[UseDates] ([ID])
);

