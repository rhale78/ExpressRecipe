CREATE TABLE [dbo].[Product] (
    [ID]                INT            IDENTITY (1, 1) NOT NULL,
    [Name]              NVARCHAR (25)  NULL,
    [Description]       NVARCHAR (255) NULL,
    [BrandID]           INT            NULL,
    [ProductCategoryID] INT            NULL,
    CONSTRAINT [PK_Product] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_Product_Brand] FOREIGN KEY ([BrandID]) REFERENCES [dbo].[Brand] ([ID]),
    CONSTRAINT [FK_Product_ProductCategory] FOREIGN KEY ([ProductCategoryID]) REFERENCES [dbo].[ProductCategory] ([ID])
);

