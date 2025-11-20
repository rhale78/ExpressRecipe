-- Seed Common Allergens
INSERT INTO Allergen (Id, Name, AlternativeNames, Description, Category)
VALUES
    (NEWID(), 'Peanuts', 'Groundnuts, Arachis hypogaea', 'Tree nut allergy - can cause severe reactions', 'Nuts'),
    (NEWID(), 'Tree Nuts', 'Almonds, Walnuts, Cashews, Pecans, Pistachios', 'Includes various tree nuts', 'Nuts'),
    (NEWID(), 'Milk', 'Dairy, Lactose, Casein, Whey', 'Dairy products from cows, goats, sheep', 'Dairy'),
    (NEWID(), 'Eggs', 'Albumin, Ovalbumin', 'Chicken eggs and egg products', 'Animal Products'),
    (NEWID(), 'Wheat', 'Gluten (from wheat), Wheat flour', 'Contains gluten protein', 'Grains'),
    (NEWID(), 'Soy', 'Soybean, Soy protein, Lecithin', 'Soybeans and soy-derived products', 'Legumes'),
    (NEWID(), 'Fish', 'Finned fish, Salmon, Tuna, Cod', 'All finned fish species', 'Seafood'),
    (NEWID(), 'Shellfish', 'Crustaceans, Shrimp, Crab, Lobster, Mollusks', 'Crustaceans and mollusks', 'Seafood'),
    (NEWID(), 'Sesame', 'Sesame seeds, Tahini, Sesame oil', 'Sesame seeds and derivatives', 'Seeds'),
    (NEWID(), 'Sulfites', 'Sulphur dioxide, Sodium sulfite', 'Preservatives used in wine, dried fruit', 'Additives'),
    (NEWID(), 'Mustard', 'Mustard seeds, Mustard powder', 'Mustard plant seeds and products', 'Spices'),
    (NEWID(), 'Celery', 'Celeriac, Celery seeds', 'Celery and celery seeds', 'Vegetables'),
    (NEWID(), 'Lupin', 'Lupine, Lupin flour', 'Legume used in some flours', 'Legumes'),
    (NEWID(), 'Gluten', 'Wheat gluten, Barley, Rye', 'Protein found in wheat, barley, rye', 'Grains');
GO

-- Seed Common Dietary Restrictions
INSERT INTO DietaryRestriction (Id, Name, Type, Description, CommonExclusions)
VALUES
    (NEWID(), 'Vegetarian', 'Ethical', 'No meat or fish', 'Meat, poultry, fish, seafood, gelatin'),
    (NEWID(), 'Vegan', 'Ethical', 'No animal products', 'Meat, poultry, fish, seafood, dairy, eggs, honey, gelatin'),
    (NEWID(), 'Pescatarian', 'Preference', 'Vegetarian plus fish/seafood', 'Meat, poultry'),
    (NEWID(), 'Gluten-Free', 'Medical', 'No gluten-containing grains', 'Wheat, barley, rye, malt, brewer''s yeast'),
    (NEWID(), 'Dairy-Free', 'Medical', 'No dairy products', 'Milk, cheese, butter, yogurt, cream, whey, casein'),
    (NEWID(), 'Kosher', 'Religious', 'Jewish dietary laws', 'Pork, shellfish, mixing meat and dairy'),
    (NEWID(), 'Halal', 'Religious', 'Islamic dietary laws', 'Pork, alcohol, non-halal meat'),
    (NEWID(), 'Paleo', 'Health', 'Paleolithic diet', 'Grains, legumes, dairy, processed foods, refined sugar'),
    (NEWID(), 'Keto', 'Health', 'Low-carb, high-fat diet', 'Grains, sugar, most fruits, starchy vegetables'),
    (NEWID(), 'Low-FODMAP', 'Medical', 'Reduces fermentable carbs for IBS', 'Onions, garlic, wheat, beans, certain fruits'),
    (NEWID(), 'Nut-Free', 'Medical', 'No tree nuts or peanuts', 'All tree nuts, peanuts, nut oils'),
    (NEWID(), 'Diabetic-Friendly', 'Medical', 'Low glycemic index foods', 'High sugar foods, refined carbs'),
    (NEWID(), 'Low-Sodium', 'Medical', 'Reduced salt intake', 'Processed foods, canned goods, cured meats, soy sauce'),
    (NEWID(), 'Heart-Healthy', 'Health', 'Low saturated fat and cholesterol', 'Fried foods, fatty meats, full-fat dairy'),
    (NEWID(), 'Renal-Friendly', 'Medical', 'Kidney disease diet', 'High potassium, phosphorus, sodium foods');
GO
