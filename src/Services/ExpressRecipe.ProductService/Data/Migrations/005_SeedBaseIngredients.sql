-- Migration: 005_SeedBaseIngredients
-- Description: Seed common base ingredients
-- Date: 2024-11-19

-- Grains & Flours
INSERT INTO BaseIngredient (Name, Category, Description, Purpose, IsAllergen, AllergenType, IsApproved) VALUES
('Wheat Flour', 'Grain', 'Ground wheat grain, primary flour for baking', 'Structure, thickening, binding', 1, 'Gluten', 1),
('Whole Wheat Flour', 'Grain', 'Flour made from whole wheat kernel including bran and germ', 'Structure, fiber, nutrients', 1, 'Gluten', 1),
('Rice Flour', 'Grain', 'Ground rice, gluten-free flour alternative', 'Thickening, gluten-free baking', 0, NULL, 1),
('Corn Flour', 'Grain', 'Finely ground corn', 'Thickening, breading, baking', 0, NULL, 1),
('Oat Flour', 'Grain', 'Ground oats', 'Baking, texture, fiber', 0, NULL, 1),
('White Rice', 'Grain', 'Polished rice grain', 'Carbohydrate base, texture', 0, NULL, 1),
('Brown Rice', 'Grain', 'Whole grain rice with bran layer', 'Carbohydrate base, fiber', 0, NULL, 1),
('Quinoa', 'Grain', 'Protein-rich pseudo-grain', 'Protein, complete amino acids', 0, NULL, 1),
('Oats', 'Grain', 'Whole oat groats or rolled oats', 'Fiber, texture, nutrients', 0, NULL, 1);
GO

-- Sweeteners
INSERT INTO BaseIngredient (Name, Category, Description, Purpose, IsAllergen, IsApproved) VALUES
('Sugar', 'Sweetener', 'Refined white granulated sugar from sugar cane or beets', 'Sweetening, texture, preservation', 0, 1),
('Brown Sugar', 'Sweetener', 'Sugar with molasses content', 'Sweetening, moisture, flavor', 0, 1),
('Honey', 'Sweetener', 'Natural bee-produced sweetener', 'Sweetening, moisture, antimicrobial', 0, 1),
('Maple Syrup', 'Sweetener', 'Natural tree sap syrup', 'Sweetening, flavor', 0, 1),
('Corn Syrup', 'Sweetener', 'Glucose syrup from corn', 'Sweetening, moisture retention', 0, 1),
('High Fructose Corn Syrup', 'Sweetener', 'Corn syrup with enzymatically converted fructose', 'Sweetening, preservation', 0, 1),
('Molasses', 'Sweetener', 'Dark syrup from sugar refining', 'Sweetening, color, flavor', 0, 1);
GO

-- Fats & Oils
INSERT INTO BaseIngredient (Name, Category, Description, Purpose, IsAllergen, IsApproved) VALUES
('Butter', 'Fat/Oil', 'Dairy fat from milk', 'Flavor, texture, moisture', 1, 'Dairy', 1),
('Vegetable Oil', 'Fat/Oil', 'Refined oil from various plants', 'Cooking, moisture, texture', 0, 1),
('Olive Oil', 'Fat/Oil', 'Oil pressed from olives', 'Cooking, flavor, healthy fats', 0, 1),
('Coconut Oil', 'Fat/Oil', 'Oil extracted from coconut meat', 'Cooking, flavor, texture', 0, 1),
('Canola Oil', 'Fat/Oil', 'Oil from rapeseed plant', 'Cooking, neutral flavor', 0, 1),
('Palm Oil', 'Fat/Oil', 'Oil from palm fruit', 'Texture, stability, preservation', 0, 1),
('Soybean Oil', 'Fat/Oil', 'Oil extracted from soybeans', 'Cooking, emulsifying', 1, 'Soy', 1);
GO

-- Dairy
INSERT INTO BaseIngredient (Name, Category, Description, Purpose, IsAllergen, AllergenType, IsApproved) VALUES
('Milk', 'Dairy', 'Liquid dairy from cows', 'Moisture, protein, calcium', 1, 'Dairy', 1),
('Cream', 'Dairy', 'High-fat portion of milk', 'Richness, texture, flavor', 1, 'Dairy', 1),
('Cheese', 'Dairy', 'Cultured and aged milk product', 'Flavor, protein, texture', 1, 'Dairy', 1),
('Yogurt', 'Dairy', 'Cultured milk product', 'Protein, probiotics, moisture', 1, 'Dairy', 1),
('Whey', 'Dairy', 'Liquid remaining after cheese production', 'Protein, moisture', 1, 'Dairy', 1),
('Casein', 'Dairy', 'Primary protein in milk', 'Protein, structure', 1, 'Dairy', 1);
GO

-- Proteins
INSERT INTO BaseIngredient (Name, Category, Description, Purpose, IsAllergen, AllergenType, IsApproved) VALUES
('Chicken', 'Protein', 'Poultry meat', 'Protein, nutrition', 0, NULL, 1),
('Beef', 'Protein', 'Cattle meat', 'Protein, iron, nutrition', 0, NULL, 1),
('Pork', 'Protein', 'Pig meat', 'Protein, fat, nutrition', 0, NULL, 1),
('Fish', 'Protein', 'Various fish species', 'Protein, omega-3 fatty acids', 1, 'Fish', 1),
('Shrimp', 'Protein', 'Shellfish crustacean', 'Protein, minerals', 1, 'Shellfish', 1),
('Eggs', 'Protein', 'Chicken eggs', 'Protein, binding, leavening', 1, 'Eggs', 1),
('Tofu', 'Protein', 'Soybean curd', 'Plant protein, texture', 1, 'Soy', 1),
('Soy Protein Isolate', 'Protein', 'Concentrated soy protein', 'Protein fortification, texture', 1, 'Soy', 1);
GO

-- Vegetables
INSERT INTO BaseIngredient (Name, Category, Description, Purpose, IsAllergen, IsApproved) VALUES
('Tomato', 'Vegetable', 'Red fruit vegetable', 'Flavor, acidity, moisture', 0, 1),
('Onion', 'Vegetable', 'Bulb vegetable', 'Flavor, aroma', 0, 1),
('Garlic', 'Vegetable', 'Pungent bulb vegetable', 'Flavor, aroma, antimicrobial', 0, 1),
('Carrot', 'Vegetable', 'Root vegetable', 'Sweetness, color, nutrients', 0, 1),
('Celery', 'Vegetable', 'Stalk vegetable', 'Flavor, crunch, aromatics', 0, 1),
('Potato', 'Vegetable', 'Starchy tuber', 'Carbohydrate, texture, bulk', 0, 1),
('Lettuce', 'Vegetable', 'Leafy green vegetable', 'Texture, nutrients, freshness', 0, 1),
('Spinach', 'Vegetable', 'Leafy green vegetable', 'Nutrients, iron, vitamins', 0, 1);
GO

-- Spices & Seasonings
INSERT INTO BaseIngredient (Name, Category, Description, Purpose, IsAllergen, IsApproved) VALUES
('Salt', 'Spice', 'Sodium chloride', 'Seasoning, preservation, flavor enhancement', 0, 1),
('Black Pepper', 'Spice', 'Ground peppercorn', 'Seasoning, heat, flavor', 0, 1),
('Cinnamon', 'Spice', 'Ground bark of cinnamon tree', 'Flavor, aroma, warmth', 0, 1),
('Paprika', 'Spice', 'Ground dried peppers', 'Color, mild flavor', 0, 1),
('Cumin', 'Spice', 'Ground cumin seeds', 'Earthy flavor, aroma', 0, 1),
('Oregano', 'Spice', 'Dried oregano herb', 'Flavor, aroma', 0, 1),
('Basil', 'Spice', 'Dried basil herb', 'Flavor, aroma', 0, 1),
('Thyme', 'Spice', 'Dried thyme herb', 'Flavor, aroma', 0, 1);
GO

-- Additives & Leavening Agents
INSERT INTO BaseIngredient (Name, Category, Description, Purpose, IsAdditive, AdditiveCode, IsApproved) VALUES
('Baking Soda', 'Additive', 'Sodium bicarbonate', 'Leavening, alkalinity', 1, 'E500', 1),
('Baking Powder', 'Additive', 'Leavening agent mixture', 'Leavening, rise', 1, NULL, 1),
('Yeast', 'Additive', 'Active microorganism for fermentation', 'Leavening, flavor development', 0, NULL, 1),
('Citric Acid', 'Additive', 'Natural acid from citrus', 'Acidity, preservation, flavor', 1, 'E330', 1),
('Ascorbic Acid', 'Additive', 'Vitamin C', 'Antioxidant, dough strengthening, preservation', 1, 'E300', 1),
('Lecithin', 'Additive', 'Emulsifier from soy or egg', 'Emulsifying, texture', 1, 'E322', 1),
('Xanthan Gum', 'Additive', 'Polysaccharide thickener', 'Thickening, stabilizing', 1, 'E415', 1),
('Guar Gum', 'Additive', 'Plant-based thickener', 'Thickening, binding', 1, 'E412', 1),
('Sodium Benzoate', 'Additive', 'Preservative', 'Preservation, antimicrobial', 1, 'E211', 1),
('Potassium Sorbate', 'Additive', 'Preservative', 'Preservation, antimicrobial', 1, 'E202', 1);
GO

-- Vitamins & Fortification (common in enriched flour)
INSERT INTO BaseIngredient (Name, Category, Description, Purpose, IsAdditive, AdditiveCode, IsApproved) VALUES
('Niacin', 'Additive', 'Vitamin B3', 'Vitamin fortification', 1, 'E375', 1),
('Iron', 'Additive', 'Mineral supplement', 'Mineral fortification', 1, NULL, 1),
('Thiamine Mononitrate', 'Additive', 'Vitamin B1', 'Vitamin fortification', 1, NULL, 1),
('Riboflavin', 'Additive', 'Vitamin B2', 'Vitamin fortification, color', 1, 'E101', 1),
('Folic Acid', 'Additive', 'Vitamin B9', 'Vitamin fortification', 1, NULL, 1);
GO
