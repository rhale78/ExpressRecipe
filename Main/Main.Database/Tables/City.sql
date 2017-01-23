CREATE TABLE [dbo].[City] (
    [ID]   INT        IDENTITY (1, 1) NOT NULL,
    [Name] NCHAR (10) NULL,
    CONSTRAINT [PK_City] PRIMARY KEY CLUSTERED ([ID] ASC)
);

