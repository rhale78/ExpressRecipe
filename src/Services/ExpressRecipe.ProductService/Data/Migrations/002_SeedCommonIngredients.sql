-- Seed Common Ingredients

-- Grains and Flours
INSERT INTO Ingredient (Id, Name, AlternativeNames, Category, IsCommonAllergen)
VALUES
    (NEWID(), 'Wheat Flour', 'All-purpose flour, Enriched flour', 'Grains', 1),
    (NEWID(), 'Rice', 'White rice, Brown rice, Jasmine rice', 'Grains', 0),
    (NEWID(), 'Oats', 'Rolled oats, Steel-cut oats, Oatmeal', 'Grains', 0),
    (NEWID(), 'Corn', 'Maize, Cornmeal, Corn flour', 'Grains', 0),
    (NEWID(), 'Barley', 'Pearl barley, Hulled barley', 'Grains', 0);
GO

-- Dairy Products
INSERT INTO Ingredient (Id, Name, AlternativeNames, Category, IsCommonAllergen)
VALUES
    (NEWID(), 'Milk', 'Whole milk, Skim milk, 2% milk', 'Dairy', 1),
    (NEWID(), 'Cheese', 'Cheddar, Mozzarella, Parmesan', 'Dairy', 1),
    (NEWID(), 'Butter', 'Salted butter, Unsalted butter', 'Dairy', 1),
    (NEWID(), 'Yogurt', 'Greek yogurt, Plain yogurt', 'Dairy', 1),
    (NEWID(), 'Cream', 'Heavy cream, Whipping cream, Half-and-half', 'Dairy', 1);
GO

-- Proteins
INSERT INTO Ingredient (Id, Name, AlternativeNames, Category, IsCommonAllergen)
VALUES
    (NEWID(), 'Chicken', 'Chicken breast, Chicken thigh', 'Meat', 0),
    (NEWID(), 'Beef', 'Ground beef, Steak, Roast', 'Meat', 0),
    (NEWID(), 'Pork', 'Pork chops, Bacon, Ham', 'Meat', 0),
    (NEWID(), 'Eggs', 'Whole eggs, Egg whites, Egg yolks', 'Animal Products', 1),
    (NEWID(), 'Salmon', 'Atlantic salmon, Sockeye salmon', 'Seafood', 1),
    (NEWID(), 'Tuna', 'Canned tuna, Fresh tuna', 'Seafood', 1),
    (NEWID(), 'Shrimp', 'Prawns', 'Seafood', 1),
    (NEWID(), 'Tofu', 'Bean curd, Silken tofu, Firm tofu', 'Soy', 1);
GO

-- Legumes and Nuts
INSERT INTO Ingredient (Id, Name, AlternativeNames, Category, IsCommonAllergen)
VALUES
    (NEWID(), 'Peanuts', 'Groundnuts', 'Nuts', 1),
    (NEWID(), 'Almonds', 'Almond flour, Almond butter', 'Nuts', 1),
    (NEWID(), 'Cashews', 'Cashew butter', 'Nuts', 1),
    (NEWID(), 'Walnuts', 'Black walnuts, English walnuts', 'Nuts', 1),
    (NEWID(), 'Soybeans', 'Edamame, Soy milk, Soy sauce', 'Legumes', 1),
    (NEWID(), 'Black Beans', 'Turtle beans', 'Legumes', 0),
    (NEWID(), 'Chickpeas', 'Garbanzo beans, Hummus', 'Legumes', 0),
    (NEWID(), 'Lentils', 'Red lentils, Green lentils', 'Legumes', 0);
GO

-- Vegetables
INSERT INTO Ingredient (Id, Name, AlternativeNames, Category, IsCommonAllergen)
VALUES
    (NEWID(), 'Tomatoes', 'Cherry tomatoes, Roma tomatoes', 'Vegetables', 0),
    (NEWID(), 'Onions', 'Yellow onion, Red onion, White onion', 'Vegetables', 0),
    (NEWID(), 'Garlic', 'Garlic cloves, Minced garlic', 'Vegetables', 0),
    (NEWID(), 'Carrots', 'Baby carrots, Carrot sticks', 'Vegetables', 0),
    (NEWID(), 'Celery', 'Celery stalks', 'Vegetables', 1),
    (NEWID(), 'Potatoes', 'Russet potatoes, Red potatoes, Sweet potatoes', 'Vegetables', 0),
    (NEWID(), 'Broccoli', 'Broccoli florets', 'Vegetables', 0),
    (NEWID(), 'Spinach', 'Baby spinach, Frozen spinach', 'Vegetables', 0);
GO

-- Fruits
INSERT INTO Ingredient (Id, Name, AlternativeNames, Category, IsCommonAllergen)
VALUES
    (NEWID(), 'Apples', 'Granny Smith, Fuji, Gala', 'Fruits', 0),
    (NEWID(), 'Bananas', NULL, 'Fruits', 0),
    (NEWID(), 'Oranges', 'Navel oranges, Valencia oranges', 'Fruits', 0),
    (NEWID(), 'Strawberries', 'Fresh strawberries, Frozen strawberries', 'Fruits', 0),
    (NEWID(), 'Blueberries', 'Fresh blueberries, Frozen blueberries', 'Fruits', 0);
GO

-- Seasonings and Condiments
INSERT INTO Ingredient (Id, Name, AlternativeNames, Category, IsCommonAllergen)
VALUES
    (NEWID(), 'Salt', 'Table salt, Sea salt, Kosher salt', 'Seasonings', 0),
    (NEWID(), 'Black Pepper', 'Ground black pepper, Peppercorns', 'Seasonings', 0),
    (NEWID(), 'Sugar', 'Granulated sugar, White sugar', 'Sweeteners', 0),
    (NEWID(), 'Olive Oil', 'Extra virgin olive oil, Virgin olive oil', 'Oils', 0),
    (NEWID(), 'Vegetable Oil', 'Canola oil, Sunflower oil', 'Oils', 0),
    (NEWID(), 'Vinegar', 'White vinegar, Apple cider vinegar, Balsamic vinegar', 'Condiments', 0),
    (NEWID(), 'Soy Sauce', 'Tamari, Shoyu', 'Condiments', 1),
    (NEWID(), 'Mustard', 'Yellow mustard, Dijon mustard', 'Condiments', 1);
GO
