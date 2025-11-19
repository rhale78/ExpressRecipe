CREATE TABLE [dbo].[CountryPhonePrefix] (
    [ID]          INT           IDENTITY (1, 1) NOT NULL,
    [CountryID]   INT           NULL,
    [PhonePrefix] NVARCHAR (10) NULL,
    CONSTRAINT [PK_CountryPhonePrefix] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_CountryPhonePrefix_Country] FOREIGN KEY ([CountryID]) REFERENCES [dbo].[Country] ([ID])
);

