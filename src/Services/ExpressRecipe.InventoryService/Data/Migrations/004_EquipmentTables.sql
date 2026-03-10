-- Migration: 004_EquipmentTables
-- Description: Equipment templates, instances, and capabilities for EQ1
-- Date: 2026-03-09

-- EquipmentTemplate: catalog of known appliance types
CREATE TABLE EquipmentTemplate (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name         NVARCHAR(200) NOT NULL,
    Category     NVARCHAR(100) NOT NULL, -- Refrigeration, Cooking, Baking, etc.
    Description  NVARCHAR(MAX) NULL,
    IsActive     BIT NOT NULL DEFAULT 1,
    CreatedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_EquipmentTemplate_Category ON EquipmentTemplate(Category);
GO

-- EquipmentCapabilityDef: capabilities a template can have
CREATE TABLE EquipmentCapabilityDef (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TemplateId   UNIQUEIDENTIFIER NOT NULL,
    CapabilityName NVARCHAR(100) NOT NULL,
    IsDefault    BIT NOT NULL DEFAULT 1, -- pre-selected in UI
    CONSTRAINT FK_EqCapDef_Template FOREIGN KEY (TemplateId) REFERENCES EquipmentTemplate(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_EqCapDef UNIQUE (TemplateId, CapabilityName)
);
CREATE INDEX IX_EqCapDef_TemplateId ON EquipmentCapabilityDef(TemplateId);
GO

-- EquipmentInstance: user's actual equipment
CREATE TABLE EquipmentInstance (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId     UNIQUEIDENTIFIER NOT NULL,
    AddressId       UNIQUEIDENTIFIER NULL,
    TemplateId      UNIQUEIDENTIFIER NOT NULL,
    CustomName      NVARCHAR(200) NULL,
    Brand           NVARCHAR(200) NULL,
    ModelNumber     NVARCHAR(100) NULL,
    SizeValue       DECIMAL(10,2) NULL,
    SizeUnit        NVARCHAR(50) NULL,
    Notes           NVARCHAR(MAX) NULL,
    IsActive        BIT NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2 NULL,
    CONSTRAINT FK_EqInstance_Template FOREIGN KEY (TemplateId) REFERENCES EquipmentTemplate(Id),
    CONSTRAINT FK_EqInstance_Household FOREIGN KEY (HouseholdId) REFERENCES Household(Id)
);
CREATE INDEX IX_EqInstance_HouseholdId ON EquipmentInstance(HouseholdId);
GO

-- EquipmentInstanceCapability: capabilities enabled on a user's equipment instance
CREATE TABLE EquipmentInstanceCapability (
    Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    InstanceId    UNIQUEIDENTIFIER NOT NULL,
    CapabilityName NVARCHAR(100) NOT NULL,
    CONSTRAINT FK_EqInstCap_Instance FOREIGN KEY (InstanceId) REFERENCES EquipmentInstance(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_EqInstCap UNIQUE (InstanceId, CapabilityName)
);
CREATE INDEX IX_EqInstCap_InstanceId ON EquipmentInstanceCapability(InstanceId);
GO
