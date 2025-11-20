-- Migration: 002_SeedRecipeTags
-- Description: Seed common recipe tags
-- Date: 2024-11-19

-- Dietary Tags
INSERT INTO RecipeTag (Name, Description) VALUES
('Vegetarian', 'Contains no meat or fish'),
('Vegan', 'Contains no animal products'),
('Gluten-Free', 'Contains no gluten ingredients'),
('Dairy-Free', 'Contains no dairy products'),
('Nut-Free', 'Contains no nuts'),
('Egg-Free', 'Contains no eggs'),
('Low-Carb', 'Lower carbohydrate content'),
('Keto', 'Ketogenic diet friendly'),
('Paleo', 'Paleo diet friendly'),
('Whole30', 'Whole30 compliant');
GO

-- Meal Type Tags
INSERT INTO RecipeTag (Name, Description) VALUES
('Breakfast', 'Suitable for breakfast'),
('Lunch', 'Suitable for lunch'),
('Dinner', 'Suitable for dinner'),
('Snack', 'Suitable as a snack'),
('Dessert', 'Sweet dessert recipe'),
('Appetizer', 'Starter or appetizer'),
('Side Dish', 'Accompaniment to main course'),
('Main Course', 'Primary dish of meal');
GO

-- Cooking Method Tags
INSERT INTO RecipeTag (Name, Description) VALUES
('Baked', 'Cooked in oven'),
('Grilled', 'Cooked on grill'),
('Fried', 'Cooked in oil'),
('Slow Cooker', 'Made in slow cooker'),
('Instant Pot', 'Made in instant pot/pressure cooker'),
('No-Cook', 'Requires no cooking'),
('One-Pot', 'Made in single pot or pan'),
('Air Fryer', 'Made in air fryer');
GO

-- Special Occasion Tags
INSERT INTO RecipeTag (Name, Description) VALUES
('Holiday', 'Suitable for holidays'),
('Party', 'Great for gatherings'),
('Kid-Friendly', 'Appeals to children'),
('Budget-Friendly', 'Economical ingredients'),
('Meal Prep', 'Good for meal prepping'),
('Quick & Easy', 'Fast preparation time'),
('Comfort Food', 'Hearty comfort food'),
('Healthy', 'Nutritious and balanced');
GO

-- Cuisine Tags
INSERT INTO RecipeTag (Name, Description) VALUES
('Italian', 'Italian cuisine'),
('Mexican', 'Mexican cuisine'),
('Asian', 'Asian cuisine'),
('Mediterranean', 'Mediterranean cuisine'),
('American', 'American cuisine'),
('French', 'French cuisine'),
('Indian', 'Indian cuisine');
GO
