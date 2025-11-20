CREATE TABLE [dbo].[ProductInstanceRawData] (
    [ID]                   INT            IDENTITY (1, 1) NOT NULL,
    [ProductID]            INT            NULL,
    [Name]                 NVARCHAR (75)  NULL,
    [Description]          NVARCHAR (255) NULL,
    [IngredientsData]      NVARCHAR (MAX) NULL,
    [ProductSizeData]      NVARCHAR (MAX) NULL,
    [NutritionData]        NVARCHAR (MAX) NULL,
    [AllergyWarningData]   NVARCHAR (MAX) NULL,
    [UPC]                  NVARCHAR (50)  NULL,
    [CreatedUpdatedDataID] INT            NULL,
    [ProductInstanceID]    INT            NULL,
    CONSTRAINT [PK_ProductInstanceRawData] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_ProductInstanceRawData_Product] FOREIGN KEY ([ProductID]) REFERENCES [dbo].[Product] ([ID]),
    CONSTRAINT [FK_ProductInstanceRawData_ProductInstance] FOREIGN KEY ([ProductInstanceID]) REFERENCES [dbo].[ProductInstance] ([ID])
);

