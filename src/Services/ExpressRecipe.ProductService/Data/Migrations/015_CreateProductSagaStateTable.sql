-- Migration: 015_CreateProductSagaStateTable
-- Description: Create table for product processing saga state tracking
-- Required by SqlSagaRepository<ProductProcessingSagaState>

CREATE TABLE ProductProcessingSagaState (
    CorrelationId NVARCHAR(128) NOT NULL,
    CurrentMask BIGINT NOT NULL DEFAULT 0,
    Status INT NOT NULL DEFAULT 0,
    StartedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CompletedAt DATETIMEOFFSET NULL,
    StateJson NVARCHAR(MAX) NOT NULL,
    
    CONSTRAINT PK_ProductProcessingSagaState PRIMARY KEY (CorrelationId)
);
GO

CREATE INDEX IX_ProductProcessingSagaState_Status ON ProductProcessingSagaState(Status);
GO
