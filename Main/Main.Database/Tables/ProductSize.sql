CREATE TABLE [dbo].[ProductSize] (
    [ID]                      INT        IDENTITY (1, 1) NOT NULL,
    [UnitAmount]              FLOAT (53) NULL,
    [StandardUnitID]          INT        NULL,
    [ProductPackageTypeID]    INT        NULL,
    [IsGroup]                 BIT        NULL,
    [IndividualProductSizeID] INT        NULL,
    CONSTRAINT [PK_ProductSize] PRIMARY KEY CLUSTERED ([ID] ASC),
    CONSTRAINT [FK_ProductSize_ProductPackageType] FOREIGN KEY ([ProductPackageTypeID]) REFERENCES [dbo].[ProductPackageType] ([ID]),
    CONSTRAINT [FK_ProductSize_ProductSize] FOREIGN KEY ([IndividualProductSizeID]) REFERENCES [dbo].[ProductSize] ([ID]),
    CONSTRAINT [FK_ProductSize_StandardUnit] FOREIGN KEY ([StandardUnitID]) REFERENCES [dbo].[StandardUnit] ([ID])
);

