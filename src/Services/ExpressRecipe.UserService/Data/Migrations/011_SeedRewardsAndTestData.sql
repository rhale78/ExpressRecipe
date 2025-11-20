-- Migration: 011_SeedRewardsAndTestData
-- Description: Seed reward items and additional test data
-- Date: 2024-11-19

-- Seed Reward Items
INSERT INTO RewardItem (Name, Description, PointsCost, RewardType, Value, ImageUrl, IsActive, QuantityAvailable) VALUES
-- Subscription Extensions
('1 Month Premium Extension', 'Extend your Premium subscription by 1 month', 1000, 'SubscriptionExtension', '1 month', NULL, 1, NULL),
('1 Month Plus Extension', 'Extend your Plus subscription by 1 month', 500, 'SubscriptionExtension', '1 month', NULL, 1, NULL),
('Upgrade to Plus (1 Month)', 'Try Plus tier for 1 month', 800, 'SubscriptionUpgrade', 'Plus - 1 month', NULL, 1, NULL),
('Upgrade to Premium (1 Month)', 'Try Premium tier for 1 month', 1500, 'SubscriptionUpgrade', 'Premium - 1 month', NULL, 1, NULL),

-- Feature Unlocks
('Advanced Recipe Import', 'Unlock recipe import from all sources for 6 months', 300, 'FeatureUnlock', 'Recipe Import - 6 months', NULL, 1, NULL),
('Advanced Reports Access', 'Access advanced reports for 3 months', 400, 'FeatureUnlock', 'Advanced Reports - 3 months', NULL, 1, NULL),
('Inventory Tracking', 'Unlock inventory tracking for 3 months', 350, 'FeatureUnlock', 'Inventory - 3 months', NULL, 1, NULL),

-- Badges
('Community Hero Badge', 'Recognition badge for top contributors', 200, 'Badge', 'Hero Badge', NULL, 1, NULL),
('Recipe Master Badge', 'Badge for creating 50+ recipes', 150, 'Badge', 'Master Badge', NULL, 1, NULL),
('Price Detective Badge', 'Badge for reporting 100+ prices', 100, 'Badge', 'Detective Badge', NULL, 1, NULL),
('7-Day Streak Badge', 'Badge for 7-day activity streak', 75, 'Badge', 'Streak Badge - 7 days', NULL, 1, NULL),
('30-Day Streak Badge', 'Badge for 30-day activity streak', 250, 'Badge', 'Streak Badge - 30 days', NULL, 1, NULL),
('365-Day Streak Badge', 'Badge for 365-day activity streak', 2000, 'Badge', 'Streak Badge - 1 year', NULL, 1, NULL),

-- Discounts
('$5 Store Credit', 'Virtual credit for in-app purchases', 500, 'Discount', '$5', NULL, 1, 100),
('$10 Store Credit', 'Virtual credit for in-app purchases', 900, 'Discount', '$10', NULL, 1, 50),
('$25 Store Credit', 'Virtual credit for in-app purchases', 2000, 'Discount', '$25', NULL, 1, 20),

-- Special Rewards
('Featured Recipe Placement', 'Have your recipe featured on the homepage for 1 week', 1000, 'Featured', '1 week homepage feature', NULL, 1, 5),
('Priority Support (1 Month)', 'Get priority customer support for 1 month', 300, 'Support', 'Priority Support - 1 month', NULL, 1, NULL),
('Ad-Free Experience (3 Months)', 'Remove all ads for 3 months', 250, 'AdFree', 'Ad-Free - 3 months', NULL, 1, NULL),
('Custom Profile Theme', 'Unlock custom profile themes and colors', 150, 'Cosmetic', 'Custom Theme', NULL, 1, NULL),
('Extra Family Member Slots', 'Add 2 additional family member slots', 200, 'FeatureUnlock', '+2 family slots', NULL, 1, NULL);
GO

-- Update ReportType with ParameterSchema (JSON schema for dynamic report configuration)
UPDATE ReportType SET ParameterSchema = '{"type":"object","properties":{"startDate":{"type":"string","format":"date"},"endDate":{"type":"string","format":"date"}}}' WHERE Category = 'Shopping';
UPDATE ReportType SET ParameterSchema = '{"type":"object","properties":{"startDate":{"type":"string","format":"date"},"endDate":{"type":"string","format":"date"},"includeFamily":{"type":"boolean"}}}' WHERE Category = 'Nutrition';
UPDATE ReportType SET ParameterSchema = '{"type":"object","properties":{"includeExpired":{"type":"boolean"},"sortBy":{"type":"string","enum":["name","expirationDate","category"]}}}' WHERE Category = 'Inventory';
UPDATE ReportType SET ParameterSchema = '{"type":"object","properties":{"startDate":{"type":"string","format":"date"},"endDate":{"type":"string","format":"date"},"groupBy":{"type":"string","enum":["day","week","month"]}}}' WHERE Category = 'Financial';
UPDATE ReportType SET ParameterSchema = '{"type":"object","properties":{"startDate":{"type":"string","format":"date"},"endDate":{"type":"string","format":"date"},"activityTypes":{"type":"array","items":{"type":"string"}}}}' WHERE Category = 'Activity';
GO

-- Add some sample activity types as reference
-- (UserActivity table uses ActivityType string, these are common values)
-- Common Activity Types:
-- Login, Logout, RecipeViewed, RecipeCreated, RecipeCooked, RecipeShared, RecipeFavorited
-- ProductViewed, ProductScanned, ProductReviewed, ProductPurchased
-- StoreSearched, StoreCheckedIn, CouponClipped, CouponRedeemed
-- MealPlanCreated, MealPlanCompleted, ShoppingListCreated, ShoppingListCompleted
-- InventoryItemAdded, InventoryItemRemoved, InventoryItemExpired
-- FriendAdded, FriendInvited, CommentPosted, CommentLiked
-- ContributionSubmitted, ContributionApproved, PointsEarned, PointsSpent, RewardRedeemed
-- ReportGenerated, SubscriptionStarted, SubscriptionCancelled, SubscriptionRenewed
GO
