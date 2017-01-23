CREATE TABLE [dbo].[PhoneNumber] (
    [ID]                   INT           IDENTITY (1, 1) NOT NULL,
    [CountryPhonePrefixID] INT           NULL,
    [AreaCodeID]           INT           NULL,
    [PhoneNumber]          NVARCHAR (10) NULL,
    [Extension]            NVARCHAR (10) NULL,
    [PhoneDeviceTypeID]    INT           NULL,
    [PhoneUseTypeID]       INT           NULL,
    CONSTRAINT [PK_PhoneNumber] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PhoneNumber_CountryPhonePrefix] FOREIGN KEY ([CountryPhonePrefixID]) REFERENCES [dbo].[CountryPhonePrefix] ([ID]),
    CONSTRAINT [FK_PhoneNumber_PhoneDeviceType] FOREIGN KEY ([PhoneDeviceTypeID]) REFERENCES [dbo].[PhoneDeviceType] ([ID]),
    CONSTRAINT [FK_PhoneNumber_PhoneUseType] FOREIGN KEY ([PhoneUseTypeID]) REFERENCES [dbo].[PhoneUseType] ([ID])
);

