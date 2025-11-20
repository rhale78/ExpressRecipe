-- Seed common cuisines
INSERT INTO Cuisine (Id, Name, Description, Region)
VALUES
    (NEWID(), 'Italian', 'Pasta, pizza, risotto, and Mediterranean flavors', 'Europe'),
    (NEWID(), 'Chinese', 'Stir-fries, dumplings, noodles, and rice dishes', 'Asia'),
    (NEWID(), 'Mexican', 'Tacos, burritos, enchiladas, and spicy flavors', 'North America'),
    (NEWID(), 'Indian', 'Curries, tandoori, biryani, and aromatic spices', 'Asia'),
    (NEWID(), 'Japanese', 'Sushi, ramen, tempura, and delicate flavors', 'Asia'),
    (NEWID(), 'Thai', 'Pad thai, curries, and sweet-spicy-sour flavors', 'Asia'),
    (NEWID(), 'French', 'Fine dining, pastries, sauces, and wine pairings', 'Europe'),
    (NEWID(), 'Greek', 'Mediterranean diet, olive oil, feta, fresh vegetables', 'Europe'),
    (NEWID(), 'American', 'Burgers, BBQ, comfort food, diverse regional styles', 'North America'),
    (NEWID(), 'Mediterranean', 'Healthy fats, seafood, vegetables, whole grains', 'Europe/Middle East'),
    (NEWID(), 'Korean', 'Kimchi, BBQ, rice dishes, and fermented foods', 'Asia'),
    (NEWID(), 'Vietnamese', 'Pho, spring rolls, fresh herbs, light flavors', 'Asia'),
    (NEWID(), 'Spanish', 'Tapas, paella, seafood, and olive oil', 'Europe'),
    (NEWID(), 'Middle Eastern', 'Hummus, falafel, kebabs, and aromatic spices', 'Middle East'),
    (NEWID(), 'Caribbean', 'Jerk seasoning, tropical fruits, rice and beans', 'Caribbean');
GO

-- Seed common health goals
INSERT INTO HealthGoal (Id, Name, Description, Category)
VALUES
    (NEWID(), 'Weight Loss', 'Reduce body weight through calorie deficit', 'Weight Management'),
    (NEWID(), 'Weight Gain', 'Increase body weight through calorie surplus', 'Weight Management'),
    (NEWID(), 'Muscle Building', 'Increase muscle mass through protein and strength training', 'Fitness'),
    (NEWID(), 'Heart Health', 'Improve cardiovascular health and reduce cholesterol', 'Health'),
    (NEWID(), 'Lower Blood Pressure', 'Reduce sodium and improve heart health', 'Health'),
    (NEWID(), 'Blood Sugar Control', 'Manage diabetes and glucose levels', 'Health'),
    (NEWID(), 'Digestive Health', 'Improve gut health and reduce digestive issues', 'Health'),
    (NEWID(), 'Energy Boost', 'Increase daily energy levels through nutrition', 'Performance'),
    (NEWID(), 'Athletic Performance', 'Optimize nutrition for sports and exercise', 'Performance'),
    (NEWID(), 'Immune Support', 'Strengthen immune system through nutrition', 'Health'),
    (NEWID(), 'Anti-Inflammatory', 'Reduce inflammation through diet', 'Health'),
    (NEWID(), 'Brain Health', 'Support cognitive function and memory', 'Health'),
    (NEWID(), 'Bone Health', 'Strengthen bones and prevent osteoporosis', 'Health'),
    (NEWID(), 'Skin Health', 'Improve skin appearance through nutrition', 'Beauty'),
    (NEWID(), 'Longevity', 'Promote healthy aging and lifespan', 'Lifestyle');
GO
