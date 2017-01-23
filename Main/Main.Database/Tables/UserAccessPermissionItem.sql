CREATE TABLE [dbo].[UserAccessPermissionItem] (
    [ID]                      INT IDENTITY (1, 1) NOT NULL,
    [UserAccessTypeID]        INT NULL,
    [PermissionItemToGroupID] INT NULL,
    CONSTRAINT [PK_UserAccessPermissionItem] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_UserAccessPermissionItem_PermissionItemToGroup] FOREIGN KEY ([PermissionItemToGroupID]) REFERENCES [dbo].[PermissionItemToGroup] ([ID]),
    CONSTRAINT [FK_UserAccessPermissionItem_UserAccessType] FOREIGN KEY ([UserAccessTypeID]) REFERENCES [dbo].[UserAccessType] ([ID])
);

