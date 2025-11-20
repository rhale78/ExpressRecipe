CREATE TABLE [dbo].[EmailAddress] (
    [ID]                    INT           IDENTITY (1, 1) NOT NULL,
    [EmailAddress]          NVARCHAR (75) NULL,
    [EmailAddressUseTypeID] INT           NULL,
    CONSTRAINT [PK_EmailAddress] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_EmailAddress_EmailAddressUseType] FOREIGN KEY ([EmailAddressUseTypeID]) REFERENCES [dbo].[EmailAddressUseType] ([ID])
);

