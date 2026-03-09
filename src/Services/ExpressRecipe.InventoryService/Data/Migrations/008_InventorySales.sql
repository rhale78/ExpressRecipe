-- Migration: 008_InventorySales
-- Description: Lightweight sales records for items sold from inventory
-- Date: 2026-03-09

CREATE TABLE InventorySale (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId     UNIQUEIDENTIFIER NOT NULL,
    InventoryItemId UNIQUEIDENTIFIER NULL,       -- NULL if item was fully consumed/removed
    ProductName     NVARCHAR(200) NOT NULL,       -- denormalized for history after item deletion
    Quantity        DECIMAL(10, 3) NOT NULL,
    Unit            NVARCHAR(50) NOT NULL,
    SaleDate        DATE NOT NULL DEFAULT CAST(GETUTCDATE() AS DATE),
    Buyer           NVARCHAR(200) NULL,           -- optional: "Neighbor", "Farmers Market", etc.
    Notes           NVARCHAR(500) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_InventorySale_HouseholdId ON InventorySale(HouseholdId, SaleDate DESC);
CREATE INDEX IX_InventorySale_ItemId      ON InventorySale(InventoryItemId);
GO
