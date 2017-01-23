CREATE TABLE [dbo].[AreaCode] (
    [ID]       INT          IDENTITY (1, 1) NOT NULL,
    [AreaCode] NVARCHAR (5) NULL,
    CONSTRAINT [PK_AreaCode] PRIMARY KEY CLUSTERED ([ID] ASC)
);

