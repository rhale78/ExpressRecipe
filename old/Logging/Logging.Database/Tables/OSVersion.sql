CREATE TABLE [dbo].[OSVersion] (
    [ID]            INT            IDENTITY (1, 1) NOT NULL,
    [Name]          NVARCHAR (75)  NOT NULL,
    [BrandID]       INT            NOT NULL,
    [VersionString] NVARCHAR (75)  NOT NULL,
    [Description]   NVARCHAR (255) NULL,
    CONSTRAINT [PK_OSVersion] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_OSVersion_Brand] FOREIGN KEY ([BrandID]) REFERENCES [dbo].[Brand] ([ID])
);

