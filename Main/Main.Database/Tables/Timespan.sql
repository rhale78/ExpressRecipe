CREATE TABLE [dbo].[Timespan] (
    [ID]             INT           IDENTITY (1, 1) NOT NULL,
    [TimespanValue]  NVARCHAR (10) NULL,
    [TimespanTypeID] INT           NULL,
    CONSTRAINT [PK_Timespan] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_Timespan_TimespanType] FOREIGN KEY ([TimespanTypeID]) REFERENCES [dbo].[TimespanType] ([ID])
);

