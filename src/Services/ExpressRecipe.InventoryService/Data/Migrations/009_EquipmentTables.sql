-- Migration: 009_EquipmentTables
-- Description: Create equipment management tables (templates, instances, capabilities)
-- Date: 2026-03-09

CREATE TABLE EquipmentTemplate (
    Id        UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name      NVARCHAR(200) NOT NULL,
    Category  NVARCHAR(100) NOT NULL,  -- Appliance|Cookware|Storage|Preservation
    IsBuiltIn BIT NOT NULL DEFAULT 1,
    IsActive  BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
CREATE UNIQUE INDEX UQ_EquipmentTemplate_Name ON EquipmentTemplate(Name) WHERE IsBuiltIn=1;
GO

CREATE TABLE EquipmentTemplateCapability (
    Id         UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TemplateId UNIQUEIDENTIFIER NOT NULL,
    Capability NVARCHAR(100) NOT NULL,
    CONSTRAINT FK_ETC_Template FOREIGN KEY (TemplateId)
        REFERENCES EquipmentTemplate(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_ETC_TemplateCapability UNIQUE (TemplateId, Capability)
);
GO

CREATE TABLE EquipmentInstance (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    HouseholdId  UNIQUEIDENTIFIER NOT NULL,
    AddressId    UNIQUEIDENTIFIER NULL,
    TemplateId   UNIQUEIDENTIFIER NULL,
    CustomName   NVARCHAR(200) NULL,
    Brand        NVARCHAR(100) NULL,
    ModelNumber  NVARCHAR(200) NULL,
    SizeValue    DECIMAL(10,2) NULL,
    SizeUnit     NVARCHAR(20) NULL,     -- qt|L|cups|gal|cu_ft
    Notes        NVARCHAR(500) NULL,
    IsActive     BIT NOT NULL DEFAULT 1,
    CreatedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt    DATETIME2 NULL,
    CONSTRAINT FK_EI_Household FOREIGN KEY (HouseholdId) REFERENCES Household(Id) ON DELETE CASCADE,
    CONSTRAINT FK_EI_Template  FOREIGN KEY (TemplateId)  REFERENCES EquipmentTemplate(Id)
);
CREATE INDEX IX_EquipmentInstance_HouseholdId ON EquipmentInstance(HouseholdId, IsActive);
GO

CREATE TABLE EquipmentInstanceCapability (
    Id         UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    InstanceId UNIQUEIDENTIFIER NOT NULL,
    Capability NVARCHAR(100) NOT NULL,
    CONSTRAINT FK_EIC_Instance FOREIGN KEY (InstanceId)
        REFERENCES EquipmentInstance(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_EIC_InstanceCapability UNIQUE (InstanceId, Capability)
);
GO
