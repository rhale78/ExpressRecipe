-- Migration: 001_CreateAIProviderConfig
-- Description: AI provider configuration table for routing use-cases to model/provider

CREATE TABLE AIProviderConfig (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UseCase     NVARCHAR(100) NOT NULL,
    Provider    NVARCHAR(50)  NOT NULL,   -- Ollama | OpenAI | AzureOpenAI
    ModelName   NVARCHAR(100) NOT NULL,
    IsActive    BIT NOT NULL DEFAULT 1,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
CREATE UNIQUE INDEX IX_AIProviderConfig_UseCase ON AIProviderConfig(UseCase) WHERE IsActive = 1;
GO
