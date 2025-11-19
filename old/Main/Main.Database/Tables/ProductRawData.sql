CREATE TABLE [dbo].[ProductRawData] (
    [ID]                   INT            IDENTITY (1, 1) NOT NULL,
    [Name]                 NVARCHAR (75)  NULL,
    [Description]          NVARCHAR (255) NULL,
    [BrandData]            NVARCHAR (MAX) NULL,
    [ProductCategoryData]  NVARCHAR (MAX) NULL,
    [CreatedUpdatedDataID] INT            NULL,
    [ProductID]            INT            NULL,
    CONSTRAINT [PK_ProductRawData] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_ProductRawData_CreatedUpdatedData] FOREIGN KEY ([CreatedUpdatedDataID]) REFERENCES [dbo].[CreatedUpdatedData] ([ID]),
    CONSTRAINT [FK_ProductRawData_Product] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[Product] ([ID])
);

