CREATE TABLE [dbo].[Component] (
    [ID]                    INT            IDENTITY (1, 1) NOT NULL,
    [Path]                  NVARCHAR (255) NULL,
    [Filename]              NVARCHAR (255) NULL,
    [Name]                  NVARCHAR (75)  NULL,
    [ComponentTypeID]       INT            NOT NULL,
    [ApplicationVersionID]  INT            NOT NULL,
    [FileAssemblyVersionID] INT            NOT NULL,
    CONSTRAINT [PK_Component] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_Component_ApplicationVersion] FOREIGN KEY ([ApplicationVersionID]) REFERENCES [dbo].[ApplicationVersion] ([ID]),
    CONSTRAINT [FK_Component_ComponentType] FOREIGN KEY ([ComponentTypeID]) REFERENCES [dbo].[ComponentType] ([ID]),
    CONSTRAINT [FK_Component_FileAssemblyVersion] FOREIGN KEY ([FileAssemblyVersionID]) REFERENCES [dbo].[FileAssemblyVersion] ([ID])
);

