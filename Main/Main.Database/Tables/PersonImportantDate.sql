CREATE TABLE [dbo].[PersonImportantDate] (
    [ID]                  INT  IDENTITY (1, 1) NOT NULL,
    [ImportantDateTypeID] INT  NULL,
    [Date]                DATE NULL,
    CONSTRAINT [PK_PersonImportantDate] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonImportantDate_PersonImportantDateType] FOREIGN KEY ([ImportantDateTypeID]) REFERENCES [dbo].[PersonImportantDateType] ([ID])
);

