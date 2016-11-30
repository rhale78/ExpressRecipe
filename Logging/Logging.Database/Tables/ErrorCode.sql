CREATE TABLE [dbo].[ErrorCode] (
    [ID]                    INT            IDENTITY (1, 1) NOT NULL,
    [ErrorCode]             INT            NOT NULL,
    [ApplicationVersionID]  INT            NOT NULL,
    [DefaultMessage]        NVARCHAR (255) NOT NULL,
    [DefaultLogTypeID]      INT            NOT NULL,
    [DefaultLogSeverityID]  INT            NOT NULL,
    [CreatedModifiedDateID] INT            NOT NULL,
    CONSTRAINT [PK_ErrorCode] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_ErrorCode_ApplicationVersion] FOREIGN KEY ([ApplicationVersionID]) REFERENCES [dbo].[ApplicationVersion] ([ID]),
    CONSTRAINT [FK_ErrorCode_CreatedModifiedDates] FOREIGN KEY ([CreatedModifiedDateID]) REFERENCES [dbo].[CreatedModifiedDates] ([ID]),
    CONSTRAINT [FK_ErrorCode_LogSeverity] FOREIGN KEY ([DefaultLogSeverityID]) REFERENCES [dbo].[LogSeverity] ([ID]),
    CONSTRAINT [FK_ErrorCode_LogType] FOREIGN KEY ([DefaultLogTypeID]) REFERENCES [dbo].[LogType] ([ID])
);

