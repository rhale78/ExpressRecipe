CREATE TABLE [dbo].[Address] (
    [ID]               INT IDENTITY (1, 1) NOT NULL,
    [AddressTypeID]    INT NULL,
    [AddressUseTypeID] INT NULL,
    [ZipCodeID]        INT NULL,
    CONSTRAINT [PK_Address] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_Address_AddressType] FOREIGN KEY ([AddressTypeID]) REFERENCES [dbo].[AddressType] ([ID]),
    CONSTRAINT [FK_Address_AddressUse] FOREIGN KEY ([AddressUseTypeID]) REFERENCES [dbo].[AddressUse] ([ID]),
    CONSTRAINT [FK_Address_ZipCode] FOREIGN KEY ([ZipCodeID]) REFERENCES [dbo].[ZipCode] ([ID])
);

