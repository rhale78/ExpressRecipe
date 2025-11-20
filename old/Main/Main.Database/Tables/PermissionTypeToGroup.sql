CREATE TABLE [dbo].[PermissionTypeToGroup] (
    [ID]                    INT IDENTITY (1, 1) NOT NULL,
    [PermissionTypeGroupID] INT NULL,
    [PermissionTypeID]      INT NULL,
    CONSTRAINT [PK_PermissionTypeToGroup] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PermissionTypeToGroup_PermissionType] FOREIGN KEY ([PermissionTypeID]) REFERENCES [dbo].[PermissionType] ([ID]),
    CONSTRAINT [FK_PermissionTypeToGroup_PermissionTypeGroup] FOREIGN KEY ([PermissionTypeGroupID]) REFERENCES [dbo].[PermissionTypeGroup] ([ID])
);

