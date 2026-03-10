-- Migration: 003_AllergyAlertNotificationSeed
-- Description: Seed AllergyAlert notification preference for all existing users.
--   Default: InApp=1, Email=0, Push=1, SMS=0.
--   Users can opt into Email in their notification preferences.
-- Date: 2026-03-10

INSERT INTO NotificationPreference (Id, UserId, NotificationType, EnableInApp, EnableEmail, EnablePush, EnableSMS, UpdatedAt)
SELECT NEWID(), u.UserId, 'AllergyAlert', 1, 0, 1, 0, GETUTCDATE()
FROM (SELECT DISTINCT UserId FROM NotificationPreference) u
WHERE NOT EXISTS (
    SELECT 1 FROM NotificationPreference np
    WHERE np.UserId = u.UserId AND np.NotificationType = 'AllergyAlert'
);
GO
