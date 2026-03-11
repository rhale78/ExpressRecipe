-- Migration 019: AdFree subscription tier + Stripe billing columns
-- Adds the AdFree tier to the SubscriptionTier table and adds Stripe-related
-- columns to UserProfile and UserSubscription for payment-service integration.

-- ── AdFree tier seed ────────────────────────────────────────────────────────
-- Guard: only insert if the row does not already exist.
IF NOT EXISTS (SELECT 1 FROM SubscriptionTier WHERE Name = 'AdFree')
BEGIN
    INSERT INTO SubscriptionTier (
        Name, TierName, DisplayName, Description, MonthlyPrice, YearlyPrice,
        Features, MaxFamilyMembers, AllowsRecipeImport, AllowsAdvancedReports,
        AllowsInventoryTracking, AllowsPriceTracking, SupportLevel, SortOrder
    )
    VALUES (
        'AdFree', 'AdFree', 'Ad-Free',
        'Ad-free experience, same features as Free',
        1.99, 19.99,
        '["All Free features", "No advertisements"]',
        1, 0, 0, 0, 0, 'Basic', 2
    );
END;
GO

-- ── Stripe columns ───────────────────────────────────────────────────────────
-- StripeCustomerId on UserProfile (one Stripe customer per user account)
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('UserProfile') AND name = 'StripeCustomerId'
)
BEGIN
    ALTER TABLE UserProfile ADD StripeCustomerId NVARCHAR(100) NULL;
END;
GO

-- StripeSubscriptionId on UserSubscription (one Stripe subscription per active row)
IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('UserSubscription') AND name = 'StripeSubscriptionId'
)
BEGIN
    ALTER TABLE UserSubscription ADD StripeSubscriptionId NVARCHAR(100) NULL;
END;
GO
