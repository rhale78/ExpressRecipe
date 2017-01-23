CREATE TABLE [dbo].[Country] (
    [ID]      INT           IDENTITY (1, 1) NOT NULL,
    [Name]    NVARCHAR (75) NULL,
    [ISOCode] NVARCHAR (50) NULL,
    CONSTRAINT [PK_Country] PRIMARY KEY CLUSTERED ([ID] ASC)
);

