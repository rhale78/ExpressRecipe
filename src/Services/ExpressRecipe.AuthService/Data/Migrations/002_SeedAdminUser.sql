-- Migration: 002_SeedAdminUser
-- Description: Seed initial admin user
-- Date: 2026-02-19

IF NOT EXISTS (SELECT 1 FROM [User] WHERE Email = 'admin@admin.com')
BEGIN
    INSERT INTO [User] (Id, Email, PasswordHash, FirstName, LastName, EmailVerified, IsActive, CreatedAt)
    VALUES (
        '00000000-0000-0000-0000-000000000001', 
        'admin@admin.com', 
        -- Hash for 'admin' (BCrypt factor 12)
        '$2a$12$K7O0.NrpsS7m0B3yTzM8.tuAIn6W.37l/yPAtx8S0M4u9I.5v0r4G.', 
        'Admin', 
        'User', 
        1, 
        1, 
        GETUTCDATE()
    );
END
GO
