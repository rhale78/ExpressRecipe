-- Migration: 012_SeedAdminProfile
-- Description: Seed initial admin user profile
-- Date: 2026-02-19

IF NOT EXISTS (SELECT 1 FROM UserProfile WHERE UserId = '00000000-0000-0000-0000-000000000001')
BEGIN
    INSERT INTO UserProfile (Id, UserId, FirstName, LastName, SubscriptionTier, CreatedAt)
    VALUES (
        '00000000-0000-0000-0000-000000000002', 
        '00000000-0000-0000-0000-000000000001', 
        'Admin', 
        'User', 
        'Premium', 
        GETUTCDATE()
    );
END
GO
