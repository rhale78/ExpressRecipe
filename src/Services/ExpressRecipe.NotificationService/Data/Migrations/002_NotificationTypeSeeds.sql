-- Migration: 002_NotificationTypeSeeds
-- Description: Seed default NotificationPreference rows for all existing users for new notification types
-- Date: 2026-03-09
--
-- NOTE: This migration seeds preferences only for users who already have at least one
-- NotificationPreference row (i.e., users known to this service at migration time).
-- New user registration flows must ensure these preference types are seeded at account creation.

DECLARE @Types TABLE (NotificationType NVARCHAR(50), DefInApp BIT, DefEmail BIT, DefPush BIT, DefSms BIT);
INSERT INTO @Types VALUES
    ('ThawReminder',    1, 0, 1, 0),
    ('ThawEscalation',  1, 1, 1, 0),
    ('CookingTimer',    1, 0, 1, 1),
    ('StorageReminder', 1, 0, 1, 0),
    ('FreezerBurnRisk', 1, 0, 0, 0),
    ('OutageSafety',    1, 1, 1, 1);

INSERT INTO NotificationPreference (Id, UserId, NotificationType, EnableInApp, EnableEmail, EnablePush, EnableSMS, UpdatedAt)
SELECT NEWID(), u.UserId, t.NotificationType, t.DefInApp, t.DefEmail, t.DefPush, t.DefSms, GETUTCDATE()
FROM (SELECT DISTINCT UserId FROM NotificationPreference) u
CROSS JOIN @Types t
WHERE NOT EXISTS (
    SELECT 1 FROM NotificationPreference np
    WHERE np.UserId=u.UserId AND np.NotificationType=t.NotificationType
);
GO
