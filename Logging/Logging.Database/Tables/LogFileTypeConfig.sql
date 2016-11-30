CREATE TABLE [dbo].[LogFileTypeConfig] (
    [ID]                    INT            IDENTITY (1, 1) NOT NULL,
    [LogTypeConfigID]       INT            NOT NULL,
    [Path]                  NVARCHAR (255) NOT NULL,
    [FilenamePattern]       NVARCHAR (255) NOT NULL,
    [Extension]             NVARCHAR (25)  NOT NULL,
    [OverwritePolicyTypeID] INT            NOT NULL,
    [MaxLogSize]            INT            NOT NULL,
    [CreatedModifiedDateID] INT            NOT NULL,
    CONSTRAINT [PK_LogFileTypeConfig] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_LogFileTypeConfig_CreatedModifiedDates] FOREIGN KEY ([CreatedModifiedDateID]) REFERENCES [dbo].[CreatedModifiedDates] ([ID]),
    CONSTRAINT [FK_LogFileTypeConfig_LogTypeConfig] FOREIGN KEY ([LogTypeConfigID]) REFERENCES [dbo].[LogTypeConfig] ([ID]),
    CONSTRAINT [FK_LogFileTypeConfig_OverwritePolicyType] FOREIGN KEY ([OverwritePolicyTypeID]) REFERENCES [dbo].[OverwritePolicyType] ([ID])
);

