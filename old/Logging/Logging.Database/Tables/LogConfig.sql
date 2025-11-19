CREATE TABLE [dbo].[LogConfig] (
    [ID]                    INT            IDENTITY (1, 1) NOT NULL,
    [ErrorCodeID]           INT            NOT NULL,
    [ApplicationVersionID]  INT            NOT NULL,
    [Message]               NVARCHAR (255) NULL,
    [LogTypeID]             INT            NOT NULL,
    [LogSeverityID]         INT            NOT NULL,
    [CreatedModifiedDateID] INT            NOT NULL,
    [ApplicationInstanceID] INT            NULL,
    CONSTRAINT [PK_LogConfig] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_LogConfig_ApplicationInstance] FOREIGN KEY ([ApplicationInstanceID]) REFERENCES [dbo].[ApplicationInstance] ([ID]),
    CONSTRAINT [FK_LogConfig_ApplicationVersion] FOREIGN KEY ([ApplicationVersionID]) REFERENCES [dbo].[ApplicationVersion] ([ID]),
    CONSTRAINT [FK_LogConfig_CreatedModifiedDates] FOREIGN KEY ([CreatedModifiedDateID]) REFERENCES [dbo].[CreatedModifiedDates] ([ID]),
    CONSTRAINT [FK_LogConfig_ErrorCode] FOREIGN KEY ([ErrorCodeID]) REFERENCES [dbo].[ErrorCode] ([ID]),
    CONSTRAINT [FK_LogConfig_LogSeverity] FOREIGN KEY ([LogSeverityID]) REFERENCES [dbo].[LogSeverity] ([ID]),
    CONSTRAINT [FK_LogConfig_LogType] FOREIGN KEY ([LogTypeID]) REFERENCES [dbo].[LogType] ([ID])
);

