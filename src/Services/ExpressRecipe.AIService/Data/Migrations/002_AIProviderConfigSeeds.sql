-- Migration: 002_AIProviderConfigSeeds
-- Description: Seed AI provider configurations for cooking assistant use-cases

INSERT INTO AIProviderConfig (UseCase, Provider, ModelName) VALUES
('recipe-troubleshoot', 'Ollama', 'llama3.2'),
('recipe-pairings',     'Ollama', 'llama3.2'),
('recipe-variations',   'Ollama', 'llama3.2'),
('recipe-adapt',        'Ollama', 'llama3.2');
GO
