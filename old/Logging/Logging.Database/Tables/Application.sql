CREATE TABLE [dbo].[Application] (
    [ID]        INT           IDENTITY (1, 1) NOT NULL,
    [Name]      NVARCHAR (75) NOT NULL,
    [CompanyID] INT           NOT NULL,
    CONSTRAINT [PK_Application] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_Application_Company] FOREIGN KEY ([CompanyID]) REFERENCES [dbo].[Company] ([ID])
);

