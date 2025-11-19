CREATE TABLE [dbo].[MilitaryRank] (
    [ID]                 INT        IDENTITY (1, 1) NOT NULL,
    [Rank]               NCHAR (10) NULL,
    [MilitaryPaygradeID] INT        NULL,
    [MilitaryBranchID]   INT        NULL,
    CONSTRAINT [PK_MilitaryRank] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_MilitaryRank_MilitaryBranch] FOREIGN KEY ([MilitaryBranchID]) REFERENCES [dbo].[MilitaryBranch] ([ID]),
    CONSTRAINT [FK_MilitaryRank_MilitaryPaygrade] FOREIGN KEY ([MilitaryPaygradeID]) REFERENCES [dbo].[MilitaryPaygrade] ([ID])
);

