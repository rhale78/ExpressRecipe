CREATE TABLE [dbo].[PersonAddress] (
    [ID]        INT  IDENTITY (1, 1) NOT NULL,
    [PersonID]  INT  NULL,
    [AddressID] INT  NULL,
    [StartDate] DATE NULL,
    [EndDate]   DATE NULL,
    CONSTRAINT [PK_PersonAddress] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_PersonAddress_Address] FOREIGN KEY ([AddressID]) REFERENCES [dbo].[Address] ([ID]),
    CONSTRAINT [FK_PersonAddress_Person] FOREIGN KEY ([PersonID]) REFERENCES [dbo].[Person] ([ID])
);

