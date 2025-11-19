-- Migration: 006_PointsAndSubscription
-- Description: Add user points, contributions tracking, and subscription management
-- Date: 2024-11-19

-- Update UserProfile to add subscription details (already has SubscriptionTier and SubscriptionExpiresAt)
ALTER TABLE UserProfile ADD PointsBalance INT NOT NULL DEFAULT 0;
ALTER TABLE UserProfile ADD LifetimePointsEarned INT NOT NULL DEFAULT 0;
ALTER TABLE UserProfile ADD SubscriptionAutoRenew BIT NOT NULL DEFAULT 0;
ALTER TABLE UserProfile ADD PaymentMethodId NVARCHAR(100) NULL; -- External payment processor reference
GO

-- ContributionType: Types of contributions users can make
CREATE TABLE ContributionType (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(500) NULL,
    PointValue INT NOT NULL, -- Base points awarded for this contribution
    RequiresApproval BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_ContributionType_Name UNIQUE (Name)
);

-- Seed contribution types
INSERT INTO ContributionType (Name, Description, PointValue, RequiresApproval) VALUES
('Product Added', 'Add a new product to the database', 10, 1),
('Product Price Updated', 'Report current price at a store', 5, 0),
('Product Price Verified', 'Verify accuracy of existing price', 2, 0),
('Ingredient Added', 'Add a new ingredient to the database', 8, 1),
('Base Ingredient Added', 'Add a new base ingredient', 15, 1),
('Recipe Created', 'Create and share a new recipe', 20, 1),
('Recipe Photo Added', 'Add photo to existing recipe', 5, 0),
('Restaurant Added', 'Add a new restaurant location', 10, 1),
('Menu Item Added', 'Add menu item to restaurant', 5, 1),
('Coupon Submitted', 'Submit a new coupon', 5, 1),
('Product Reviewed', 'Write a product review', 3, 0),
('Recipe Reviewed', 'Write a recipe review', 3, 0),
('Restaurant Reviewed', 'Write a restaurant review', 3, 0),
('Allergen Information Updated', 'Update allergen info for product/ingredient', 10, 1),
('Nutrition Information Added', 'Add nutritional facts', 8, 1),
('Data Verification', 'Verify accuracy of community data', 2, 0),
('Bug Report', 'Report a bug or issue', 5, 0),
('Feature Suggestion', 'Suggest a new feature', 3, 0),
('Daily Login', 'Log in to the app', 1, 0),
('Weekly Active', 'Active for 7 consecutive days', 10, 0),
('Monthly Active', 'Active for 30 consecutive days', 50, 0),
('Referral', 'Refer a new user who signs up', 100, 0);
GO

-- UserContribution: Track individual user contributions
CREATE TABLE UserContribution (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    ContributionTypeId UNIQUEIDENTIFIER NOT NULL,
    PointsAwarded INT NOT NULL,
    IsApproved BIT NOT NULL DEFAULT 0,
    ApprovedBy UNIQUEIDENTIFIER NULL,
    ApprovedAt DATETIME2 NULL,
    RejectionReason NVARCHAR(MAX) NULL,
    ReferenceId UNIQUEIDENTIFIER NULL, -- ID of the entity created/modified (ProductId, RecipeId, etc.)
    ReferenceType NVARCHAR(100) NULL, -- Product, Recipe, Restaurant, etc.
    Notes NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_UserContribution_ContributionType FOREIGN KEY (ContributionTypeId)
        REFERENCES ContributionType(Id) ON DELETE CASCADE
);

CREATE INDEX IX_UserContribution_UserId ON UserContribution(UserId);
CREATE INDEX IX_UserContribution_ContributionTypeId ON UserContribution(ContributionTypeId);
CREATE INDEX IX_UserContribution_CreatedAt ON UserContribution(CreatedAt);
CREATE INDEX IX_UserContribution_ReferenceId ON UserContribution(ReferenceId);
GO

-- PointTransaction: Detailed points history (earning and spending)
CREATE TABLE PointTransaction (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    TransactionType NVARCHAR(50) NOT NULL, -- Earned, Spent, Bonus, Penalty, Expired
    PointsAmount INT NOT NULL, -- Positive for earning, negative for spending
    BalanceAfter INT NOT NULL,
    Description NVARCHAR(500) NULL,
    UserContributionId UNIQUEIDENTIFIER NULL, -- If earned from contribution
    RewardItemId UNIQUEIDENTIFIER NULL, -- If spent on reward redemption
    TransactionDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_PointTransaction_UserContribution FOREIGN KEY (UserContributionId)
        REFERENCES UserContribution(Id) ON DELETE SET NULL
);

CREATE INDEX IX_PointTransaction_UserId ON PointTransaction(UserId);
CREATE INDEX IX_PointTransaction_TransactionDate ON PointTransaction(TransactionDate);
GO

-- RewardItem: Items users can redeem with points
CREATE TABLE RewardItem (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    PointsCost INT NOT NULL,
    RewardType NVARCHAR(50) NOT NULL, -- SubscriptionExtension, FeatureUnlock, Badge, Discount, etc.
    Value NVARCHAR(100) NULL, -- "1 month", "Premium feature", etc.
    ImageUrl NVARCHAR(500) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    QuantityAvailable INT NULL, -- NULL for unlimited
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL
);

CREATE INDEX IX_RewardItem_PointsCost ON RewardItem(PointsCost);
GO

-- UserRewardRedemption: Track reward redemptions
CREATE TABLE UserRewardRedemption (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    RewardItemId UNIQUEIDENTIFIER NOT NULL,
    PointsSpent INT NOT NULL,
    RedeemedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt DATETIME2 NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_UserRewardRedemption_RewardItem FOREIGN KEY (RewardItemId)
        REFERENCES RewardItem(Id) ON DELETE CASCADE
);

CREATE INDEX IX_UserRewardRedemption_UserId ON UserRewardRedemption(UserId);
CREATE INDEX IX_UserRewardRedemption_RedeemedAt ON UserRewardRedemption(RedeemedAt);
GO

-- SubscriptionTier: Define subscription tiers and their benefits
CREATE TABLE SubscriptionTier (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(100) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    MonthlyPrice DECIMAL(10, 2) NOT NULL,
    YearlyPrice DECIMAL(10, 2) NULL,
    Features NVARCHAR(MAX) NULL, -- JSON array of features
    MaxFamilyMembers INT NOT NULL DEFAULT 1,
    MaxRecipes INT NULL, -- NULL for unlimited
    MaxMealPlans INT NULL,
    AllowsRecipeImport BIT NOT NULL DEFAULT 0,
    AllowsAdvancedReports BIT NOT NULL DEFAULT 0,
    AllowsInventoryTracking BIT NOT NULL DEFAULT 0,
    AllowsPriceTracking BIT NOT NULL DEFAULT 0,
    SupportLevel NVARCHAR(50) NULL, -- Basic, Priority, Premium
    IsActive BIT NOT NULL DEFAULT 1,
    SortOrder INT NOT NULL DEFAULT 0,
    CONSTRAINT UQ_SubscriptionTier_Name UNIQUE (Name)
);

-- Seed subscription tiers
INSERT INTO SubscriptionTier (Name, Description, MonthlyPrice, YearlyPrice, Features, MaxFamilyMembers, AllowsRecipeImport, AllowsAdvancedReports, AllowsInventoryTracking, AllowsPriceTracking, SupportLevel, SortOrder) VALUES
('Free', 'Basic features for casual users', 0.00, 0.00, '["Basic recipe search", "Allergen alerts", "Shopping lists", "Up to 50 saved recipes"]', 1, 0, 0, 0, 0, 'Basic', 1),
('Plus', 'Enhanced features for regular users', 4.99, 49.99, '["Everything in Free", "Unlimited recipes", "Meal planning", "Recipe import", "Price tracking", "Basic reports"]', 4, 1, 0, 1, 1, 'Priority', 2),
('Premium', 'Full-featured for power users and families', 9.99, 99.99, '["Everything in Plus", "Advanced reports", "Inventory tracking", "Coupon management", "Family sharing up to 8", "Priority support"]', 8, 1, 1, 1, 1, 'Premium', 3);
GO

-- SubscriptionHistory: Track subscription changes
CREATE TABLE SubscriptionHistory (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    SubscriptionTierId UNIQUEIDENTIFIER NOT NULL,
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NULL,
    BillingPeriod NVARCHAR(20) NULL, -- Monthly, Yearly
    AmountPaid DECIMAL(10, 2) NOT NULL,
    PaymentStatus NVARCHAR(50) NOT NULL, -- Paid, Pending, Failed, Refunded
    PaymentProcessor NVARCHAR(100) NULL, -- Stripe, PayPal, etc.
    TransactionId NVARCHAR(200) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CancelledAt DATETIME2 NULL,
    CancellationReason NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_SubscriptionHistory_SubscriptionTier FOREIGN KEY (SubscriptionTierId)
        REFERENCES SubscriptionTier(Id) ON DELETE CASCADE
);

CREATE INDEX IX_SubscriptionHistory_UserId ON SubscriptionHistory(UserId);
CREATE INDEX IX_SubscriptionHistory_StartDate ON SubscriptionHistory(StartDate);
GO
