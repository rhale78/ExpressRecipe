CREATE TABLE [dbo].[Version] (
    [ID]            INT           IDENTITY (1, 1) NOT NULL,
    [Major]         INT           NOT NULL,
    [Minor]         INT           NOT NULL,
    [Build]         INT           NULL,
    [Revision]      INT           NULL,
    [VersionTypeID] INT           NOT NULL,
    [Name]          NVARCHAR (75) NULL,
    CONSTRAINT [PK_Version] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_Version_VersionType] FOREIGN KEY ([VersionTypeID]) REFERENCES [dbo].[VersionType] ([ID])
);

