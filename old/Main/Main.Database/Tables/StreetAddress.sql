CREATE TABLE [dbo].[StreetAddress] (
    [ID]             INT           IDENTITY (1, 1) NOT NULL,
    [AddressID]      INT           NULL,
    [StreetToTypeID] INT           NULL,
    [StreetNumber]   NVARCHAR (50) NULL,
    [OrderIndex]     INT           NULL,
    CONSTRAINT [PK_StreetAddress] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_StreetAddress_Address] FOREIGN KEY ([AddressID]) REFERENCES [dbo].[Address] ([ID])
);

