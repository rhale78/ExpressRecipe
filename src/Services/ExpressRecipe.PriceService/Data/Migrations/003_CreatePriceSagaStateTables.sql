-- Migration: 003_CreatePriceSagaStateTables
-- Description: Create tables for price processing saga state tracking

CREATE TABLE PriceProcessingSagaState (
    CorrelationId NVARCHAR(128) NOT NULL,
    CurrentMask BIGINT NOT NULL DEFAULT 0,
    Status INT NOT NULL DEFAULT 0,
    StartedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CompletedAt DATETIMEOFFSET NULL,
    StateJson NVARCHAR(MAX) NOT NULL,
    
    CONSTRAINT PK_PriceProcessingSagaState PRIMARY KEY (CorrelationId)
);
GO

CREATE INDEX IX_PriceProcessingSagaState_Status ON PriceProcessingSagaState(Status);
GO

-- Add saga tracking to PriceObservation table
ALTER TABLE PriceObservation
    ADD SagaCorrelationId NVARCHAR(100) NULL,
        SagaStatus NVARCHAR(50) NULL DEFAULT 'Pending',
        IsProductLinked BIT NOT NULL DEFAULT 0,
        LinkedProductId UNIQUEIDENTIFIER NULL,
        ImportSessionId NVARCHAR(100) NULL;
GO

CREATE INDEX IX_PriceObservation_SagaCorrelationId ON PriceObservation(SagaCorrelationId)
    WHERE SagaCorrelationId IS NOT NULL;
GO

CREATE INDEX IX_PriceObservation_IsProductLinked ON PriceObservation(IsProductLinked);
GO
