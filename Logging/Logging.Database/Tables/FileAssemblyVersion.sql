CREATE TABLE [dbo].[FileAssemblyVersion] (
    [ID]                INT IDENTITY (1, 1) NOT NULL,
    [FileVersionID]     INT NOT NULL,
    [AssemblyVersionID] INT NOT NULL,
    CONSTRAINT [PK_FileAssemblyVersion] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_FileAssemblyVersion_Version] FOREIGN KEY ([FileVersionID]) REFERENCES [dbo].[Version] ([ID]),
    CONSTRAINT [FK_FileAssemblyVersion_Version1] FOREIGN KEY ([AssemblyVersionID]) REFERENCES [dbo].[Version] ([ID])
);

