CREATE TABLE [dbo].[ProductWarning] (
    [ID]                   INT            IDENTITY (1, 1) NOT NULL,
    [ProductInstanceID]    INT            NULL,
    [WarningText]          NVARCHAR (255) NULL,
    [ProductWarningTypeID] INT            NULL,
    CONSTRAINT [PK_ProductWarning] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_ProductWarning_ProductInstance] FOREIGN KEY ([ProductInstanceID]) REFERENCES [dbo].[ProductInstance] ([ID]),
    CONSTRAINT [FK_ProductWarning_ProductWarningType] FOREIGN KEY ([ProductWarningTypeID]) REFERENCES [dbo].[ProductWarningType] ([ID])
);

