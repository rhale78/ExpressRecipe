CREATE TABLE [dbo].[UserSocialMediaMembership] (
    [ID]                   INT            IDENTITY (1, 1) NOT NULL,
    [UserMembershipTypeID] INT            NULL,
    [ProviderUsername]     NVARCHAR (50)  NULL,
    [AccessToken]          NVARCHAR (255) NULL,
    [UserID]               INT            NULL,
    [UseDatesID]           INT            NULL,
    [LastModifiedDate]     DATE           NULL,
    CONSTRAINT [PK_UserSocialMediaMembership] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_UserSocialMediaMembership_UseDates] FOREIGN KEY ([UseDatesID]) REFERENCES [dbo].[UseDates] ([ID]),
    CONSTRAINT [FK_UserSocialMediaMembership_User] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User] ([ID]),
    CONSTRAINT [FK_UserSocialMediaMembership_UserSocialMembershipType] FOREIGN KEY ([UserMembershipTypeID]) REFERENCES [dbo].[UserSocialMembershipType] ([ID])
);

