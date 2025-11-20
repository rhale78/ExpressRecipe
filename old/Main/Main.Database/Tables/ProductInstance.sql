CREATE TABLE [dbo].[ProductInstance] (
    [ID]                       INT            IDENTITY (1, 1) NOT NULL,
    [ProductID]                INT            NULL,
    [ProductSizeID]            INT            NULL,
    [Name]                     NVARCHAR (25)  NULL,
    [Description]              NVARCHAR (255) NULL,
    [UPC]                      NVARCHAR (25)  NULL,
    [GenericProductInstanceID] INT            NULL,
    CONSTRAINT [PK_ProductInstance] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_ProductInstance_Product] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[Product] ([ID]),
    CONSTRAINT [FK_ProductInstance_ProductInstance] FOREIGN KEY ([GenericProductInstanceID]) REFERENCES [dbo].[ProductInstance] ([ID]),
    CONSTRAINT [FK_ProductInstance_ProductSize] FOREIGN KEY ([ProductSizeID]) REFERENCES [dbo].[ProductSize] ([ID])
);

