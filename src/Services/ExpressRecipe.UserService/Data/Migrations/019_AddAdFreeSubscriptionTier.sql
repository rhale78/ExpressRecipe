-- Migration: 019_AddAdFreeSubscriptionTier
-- Description: Add Ad-Free subscription tier to SubscriptionTier table
-- Date: 2026-03-10

-- Insert Ad-Free tier if it does not already exist
IF NOT EXISTS (SELECT 1 FROM SubscriptionTier WHERE TierName = 'AdFree')
BEGIN
    DECLARE @SortOrder INT = 2;
    SELECT @SortOrder = ISNULL(MAX(SortOrder), 1) + 1 FROM SubscriptionTier;

    INSERT INTO SubscriptionTier
        (Id, Name, TierName, DisplayName, MonthlyPrice, YearlyPrice,
         MaxFamilyMembers, AllowsOfflineSync, AllowsAdvancedReports,
         AllowsPriceComparison, AllowsRecipeImport, AllowsMenuPlanning,
         PointsMultiplier, IsActive, SortOrder)
    VALUES
        (NEWID(), 'AdFree', 'AdFree', 'Ad-Free',
         2.99, 24.99,
         1, 0, 0, 0, 0, 0,
         1.0, 1, @SortOrder);
END
GO
