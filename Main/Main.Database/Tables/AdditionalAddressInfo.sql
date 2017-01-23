CREATE TABLE [dbo].[AdditionalAddressInfo] (
    [ID]             INT           IDENTITY (1, 1) NOT NULL,
    [AddressID]      INT           NULL,
    [OrderIndex]     INT           NULL,
    [AdditionalInfo] NVARCHAR (75) NULL,
    CONSTRAINT [PK_AdditionalAddressInfo] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_AdditionalAddressInfo_Address] FOREIGN KEY ([AddressID]) REFERENCES [dbo].[Address] ([ID])
);

