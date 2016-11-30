CREATE TABLE [dbo].[EventLogTypeConfig] (
    [ID]                    INT           IDENTITY (1, 1) NOT NULL,
    [LogTypeConfigID]       INT           NOT NULL,
    [LogName]               NVARCHAR (50) NOT NULL,
    [Source]                NVARCHAR (50) NOT NULL,
    [MaxLogSize]            INT           NOT NULL,
    [OverwritePolicyTypeID] INT           NOT NULL,
    [CreatedModifiedDateID] INT           NOT NULL,
    CONSTRAINT [PK_EventLogTypeConfig] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_EventLogTypeConfig_CreatedModifiedDates] FOREIGN KEY ([CreatedModifiedDateID]) REFERENCES [dbo].[CreatedModifiedDates] ([ID]),
    CONSTRAINT [FK_EventLogTypeConfig_LogTypeConfig] FOREIGN KEY ([LogTypeConfigID]) REFERENCES [dbo].[LogTypeConfig] ([ID]),
    CONSTRAINT [FK_EventLogTypeConfig_OverwritePolicyType] FOREIGN KEY ([OverwritePolicyTypeID]) REFERENCES [dbo].[OverwritePolicyType] ([ID])
);

