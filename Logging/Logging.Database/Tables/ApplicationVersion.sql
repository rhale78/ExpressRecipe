CREATE TABLE [dbo].[ApplicationVersion] (
    [ID]                    INT IDENTITY (1, 1) NOT NULL,
    [ApplicationID]         INT NOT NULL,
    [FileAssemblyVersionID] INT NOT NULL,
    CONSTRAINT [PK_ApplicationVersion] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_ApplicationVersion_Application] FOREIGN KEY ([ApplicationID]) REFERENCES [dbo].[Application] ([ID]),
    CONSTRAINT [FK_ApplicationVersion_FileAssemblyVersion] FOREIGN KEY ([FileAssemblyVersionID]) REFERENCES [dbo].[FileAssemblyVersion] ([ID])
);

