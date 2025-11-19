CREATE TABLE [dbo].[PermissionItemToGroup] (
    [ID]                    INT IDENTITY (1, 1) NOT NULL,
    [PermissionItemID]      INT NULL,
    [PermissionTypeGroupID] INT NULL,
    CONSTRAINT [PK_PermissionItemToGroup] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PermissionItemToGroup_PermissionItem] FOREIGN KEY ([PermissionItemID]) REFERENCES [dbo].[PermissionItem] ([ID]),
    CONSTRAINT [FK_PermissionItemToGroup_PermissionTypeGroup] FOREIGN KEY ([PermissionTypeGroupID]) REFERENCES [dbo].[PermissionTypeGroup] ([ID])
);

