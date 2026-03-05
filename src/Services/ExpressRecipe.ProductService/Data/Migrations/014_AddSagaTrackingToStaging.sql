-- Migration: 014_AddSagaTrackingToStaging
-- Description: Add saga state tracking columns to ProductStaging table
-- These support the bit-based saga pattern for product processing pipeline

ALTER TABLE ProductStaging
    ADD SagaCorrelationId NVARCHAR(100) NULL,
        -- Stores the string name of SagaStatus enum: 'Pending', 'Running', 'Completed', 'Failed', 'Compensated'
        -- This is a denormalized display column; full saga state is in ProductProcessingSagaState table
        SagaStatus NVARCHAR(50) NULL DEFAULT 'Pending',
        SagaCurrentMask BIGINT NOT NULL DEFAULT 0,
        SagaStartedAt DATETIME2 NULL,
        SagaCompletedAt DATETIME2 NULL,
        AIVerificationPassed BIT NULL,
        AIVerificationNotes NVARCHAR(MAX) NULL,
        ImportSessionId NVARCHAR(100) NULL;
GO

CREATE INDEX IX_ProductStaging_SagaCorrelationId ON ProductStaging(SagaCorrelationId) 
    WHERE SagaCorrelationId IS NOT NULL AND IsDeleted = 0;
GO

CREATE INDEX IX_ProductStaging_ImportSessionId ON ProductStaging(ImportSessionId)
    WHERE ImportSessionId IS NOT NULL AND IsDeleted = 0;
GO
