-- Migration: 016_CreateImportSessionSagaStateTable
-- Description: Create table for import session saga state tracking

CREATE TABLE ImportSessionSagaState (
    CorrelationId NVARCHAR(128) NOT NULL,
    CurrentMask BIGINT NOT NULL DEFAULT 0,
    Status INT NOT NULL DEFAULT 0,
    StartedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CompletedAt DATETIMEOFFSET NULL,
    StateJson NVARCHAR(MAX) NOT NULL,
    
    CONSTRAINT PK_ImportSessionSagaState PRIMARY KEY (CorrelationId)
);
GO
