-- Increase precision for Quantity in RecipeIngredient to handle large values from imports
ALTER TABLE RecipeIngredient ALTER COLUMN Quantity DECIMAL(18, 4) NULL;
GO

-- Also update nutrition columns while we are at it, as some datasets have large values or high precision
ALTER TABLE RecipeNutrition ALTER COLUMN Calories DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN TotalFat DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN SaturatedFat DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN TransFat DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN Cholesterol DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN Sodium DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN TotalCarbohydrates DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN DietaryFiber DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN Sugars DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN Protein DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN VitaminD DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN Calcium DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN Iron DECIMAL(18, 4) NULL;
ALTER TABLE RecipeNutrition ALTER COLUMN Potassium DECIMAL(18, 4) NULL;
GO
