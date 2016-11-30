CREATE TABLE [dbo].[Brand] (
    [ID]        INT           IDENTITY (1, 1) NOT NULL,
    [Name]      NVARCHAR (75) NOT NULL,
    [CompanyID] INT           NOT NULL,
    CONSTRAINT [PK_Brand] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_Brand_Company] FOREIGN KEY ([CompanyID]) REFERENCES [dbo].[Company] ([ID])
);

