CREATE TABLE [dbo].[AllergySeverityProxReact] (
    [ID]                     INT           IDENTITY (1, 1) NOT NULL,
    [AllergyReactionTypeID]  INT           NULL,
    [AllergySeverityID]      INT           NULL,
    [AllergyProximityTypeID] INT           NULL,
    [Name]                   NVARCHAR (50) NULL,
    [Description]            NVARCHAR (75) NULL,
    [AllergyTimingID]        INT           NULL,
    CONSTRAINT [PK_AllergySeverityProxReact] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_AllergySeverityProxReact_AllergyProximityType] FOREIGN KEY ([AllergyProximityTypeID]) REFERENCES [dbo].[AllergyProximityType] ([ID]),
    CONSTRAINT [FK_AllergySeverityProxReact_AllergyReactionType] FOREIGN KEY ([AllergyReactionTypeID]) REFERENCES [dbo].[AllergyReactionType] ([ID]),
    CONSTRAINT [FK_AllergySeverityProxReact_AllergySeverity] FOREIGN KEY ([AllergySeverityID]) REFERENCES [dbo].[AllergySeverity] ([ID]),
    CONSTRAINT [FK_AllergySeverityProxReact_AllergyTiming] FOREIGN KEY ([AllergyTimingID]) REFERENCES [dbo].[AllergyTiming] ([ID])
);

