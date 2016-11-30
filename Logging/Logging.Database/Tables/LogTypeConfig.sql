CREATE TABLE [dbo].[LogTypeConfig] (
    [ID]                   INT IDENTITY (1, 1) NOT NULL,
    [LogTypeID]            INT NOT NULL,
    [ApplicationVersionID] INT NOT NULL,
    CONSTRAINT [PK_LogTypeConfig] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_LogTypeConfig_LogType] FOREIGN KEY ([LogTypeID]) REFERENCES [dbo].[LogType] ([ID])
);

