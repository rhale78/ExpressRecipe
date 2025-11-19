CREATE TABLE [dbo].[LogEntry] (
    [ID]                    INT             IDENTITY (1, 1) NOT NULL,
    [ErrorCodeID]           INT             NOT NULL,
    [ApplicationInstanceID] INT             NOT NULL,
    [Message]               NVARCHAR (255)  NULL,
    [Data]                  NVARCHAR (1024) NULL,
    [MessageAndData]        NVARCHAR (1024) NOT NULL,
    [CreatedModifiedDateID] INT             NOT NULL,
    CONSTRAINT [PK_LogEntry] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_LogEntry_ApplicationInstance] FOREIGN KEY ([ApplicationInstanceID]) REFERENCES [dbo].[ApplicationInstance] ([ID]),
    CONSTRAINT [FK_LogEntry_CreatedModifiedDates] FOREIGN KEY ([CreatedModifiedDateID]) REFERENCES [dbo].[CreatedModifiedDates] ([ID]),
    CONSTRAINT [FK_LogEntry_ErrorCode] FOREIGN KEY ([ErrorCodeID]) REFERENCES [dbo].[ErrorCode] ([ID])
);

