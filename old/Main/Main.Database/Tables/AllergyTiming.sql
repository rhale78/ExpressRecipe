CREATE TABLE [dbo].[AllergyTiming] (
    [ID]            INT            IDENTITY (1, 1) NOT NULL,
    [Name]          NVARCHAR (25)  NULL,
    [Description]   NVARCHAR (255) NULL,
    [MinTimespanID] INT            NULL,
    [MaxTimespanID] INT            NULL,
    CONSTRAINT [PK_AllergyTiming] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_AllergyTiming_Timespan] FOREIGN KEY ([MinTimespanID]) REFERENCES [dbo].[Timespan] ([ID]),
    CONSTRAINT [FK_AllergyTiming_Timespan1] FOREIGN KEY ([MaxTimespanID]) REFERENCES [dbo].[Timespan] ([ID])
);

