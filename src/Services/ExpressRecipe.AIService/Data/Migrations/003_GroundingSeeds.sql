-- Migration: 003_GroundingSeeds
-- Description: CookingTechniqueIssue and IngredientPairing seed tables for AI grounding

CREATE TABLE CookingTechniqueIssue (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Keywords     NVARCHAR(300) NOT NULL,   -- comma-separated match terms
    IssueName    NVARCHAR(100) NOT NULL,
    Cause        NVARCHAR(500) NOT NULL,
    Fix          NVARCHAR(500) NOT NULL,
    IsActive     BIT NOT NULL DEFAULT 1
);
CREATE INDEX IX_CookingTechniqueIssue_Keywords ON CookingTechniqueIssue(Keywords);
GO

INSERT INTO CookingTechniqueIssue (Keywords, IssueName, Cause, Fix) VALUES
(N'lumpy gravy,lumps gravy,gravy lumps,lumpy sauce',
 N'Lumpy Gravy/Sauce',
 N'Starch (cornstarch/flour) added dry to hot liquid causes it to clump instantly.',
 N'Always mix starch with equal parts COLD water or broth first to form a slurry, then whisk into the hot liquid slowly.'),
(N'too salty,over salted,too much salt,salty soup,salty stew',
 N'Over-Salted Dish',
 N'Too much salt was added during cooking or the recipe was followed too closely without tasting.',
 N'Options: 1) Add peeled raw potato chunks and simmer 15 min (absorb salt), then remove. 2) Add acid — lemon juice or white vinegar brightens and balances. 3) Dilute with unsalted broth or water and adjust other seasonings. 4) Add a starch (rice, pasta) to absorb.'),
(N'bread didnt rise,bread not rising,yeast not working,dense bread,flat bread',
 N'Bread Didn''t Rise',
 N'Yeast killed by water that was too hot (above 115°F/46°C), yeast was old/expired, or dough was too cold.',
 N'Proof yeast first: dissolve in 105–110°F (40–43°C) water with a pinch of sugar; it should foam in 5–10 min. If it doesn''t, replace the yeast. Keep dough somewhere warm (80°F/27°C) to rise.'),
(N'rubbery eggs,tough eggs,overcooked eggs,chewy eggs',
 N'Rubbery Scrambled/Fried Eggs',
 N'High heat causes proteins to tighten and expel moisture rapidly.',
 N'Use low-medium heat. For scrambled eggs, remove from heat while still slightly wet — carry-over heat finishes them. Add a splash of cream or water for fluffier texture.'),
(N'cake sank,cake collapsed,sunken cake,cake dip middle',
 N'Cake Sank in the Middle',
 N'Underbaked, oven door opened too early (cold air collapses structure), or too much leavening.',
 N'Don''t open the oven door in the first 2/3 of bake time. Test with a toothpick — must come out clean. Check oven temperature accuracy with a thermometer.'),
(N'tough steak,chewy steak,steak too chewy,rubbery steak',
 N'Tough/Chewy Steak',
 N'Cut with the grain (long muscle fibers intact) or steak was a tougher cut cooked wrong.',
 N'Always slice AGAINST the grain — perpendicular to the muscle fiber lines. For tougher cuts (chuck, flank), use low-and-slow braising instead of high-heat grilling.'),
(N'bitter coffee,bitter espresso,over extracted',
 N'Bitter Coffee/Espresso',
 N'Over-extraction from too fine a grind, too long brew time, or stale beans.',
 N'Grind coarser, reduce brew time, or use fresher beans (roasted within 2–4 weeks). For espresso, check your 25–30 second extraction window.'),
(N'greasy fried,soggy fried,fried food not crispy,oil soaked',
 N'Greasy/Soggy Fried Food',
 N'Oil temperature too low; food added while oil is cold causes absorption instead of crisping.',
 N'Heat oil to 350–375°F (175–190°C) before adding food. Don''t overcrowd the pan — it drops oil temperature. Use a thermometer. Let food drain on a wire rack, not paper towels (steam makes things soggy).'),
(N'watery sauce,thin sauce,sauce too thin,sauce not thickening',
 N'Thin/Watery Sauce',
 N'Not enough reduction time, or lid left on (trapping steam that would evaporate).',
 N'Simmer UNCOVERED over medium heat until desired consistency. Add a cornstarch slurry (1 tsp + 1 tsp cold water) for a quick thickener. Reduce heat-and-time before adding dairy.'),
(N'pasta mushy,overcooked pasta,soft pasta,pasta too soft',
 N'Overcooked Pasta',
 N'Cooked past al dente, or left sitting in hot water after draining.',
 N'Cook 1–2 minutes LESS than package time — pasta continues cooking in hot sauce. Drain and transfer directly to sauce immediately. Never rinse pasta (removes starch needed for sauce adhesion).'),
(N'dense meatballs,tough meatballs,hard meatballs',
 N'Dense/Tough Meatballs',
 N'Over-mixing develops gluten in the meat, making them rubbery.',
 N'Mix just until combined — stop the moment no dry patches remain. Use a light hand. Adding soaked breadcrumbs or ricotta also keeps them tender.'),
(N'clumped rice,sticky rice,mushy rice,gummy rice',
 N'Clumped/Mushy Rice',
 N'Stirred during cooking (releases starch), too much water, or wrong ratio.',
 N'Don''t stir once boiling. Use exact 1:2 ratio (rice:water) for long grain. Use tight-fitting lid. Let rest 5 min off heat before fluffing.'),
(N'burnt garlic,bitter garlic,garlic too dark',
 N'Burnt/Bitter Garlic',
 N'Garlic burns in seconds over high heat and turns bitter.',
 N'Add garlic over medium heat, not high. Sauté 30–60 seconds until fragrant — the moment you smell it, it''s almost done. If it browns, start over; burnt garlic cannot be saved.'),
(N'fish sticking,sticking pan,food sticking',
 N'Food Sticking to Pan',
 N'Pan or oil not hot enough before adding food, or food moved too soon.',
 N'Heat the pan first, then add oil, then add food. Food naturally releases when a crust forms — wait for it to release freely before flipping. Pat protein dry before cooking.'),
(N'flat cookies,spread cookies,cookies too thin',
 N'Cookies Spread Too Much',
 N'Butter too warm/melted, too much sugar, or no chilling.',
 N'Use room-temperature (not melted) butter. Chill dough 30–60 min before baking. Check flour measurement (spoon into cup, level off — don''t pack). Slightly underbake; they firm up cooling.');
GO

CREATE TABLE IngredientPairing (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    DishKeywords NVARCHAR(300) NOT NULL,  -- comma-separated match terms
    DishName     NVARCHAR(100) NOT NULL,
    PairingType  NVARCHAR(30) NOT NULL,   -- Vegetable|Starch|Bread|Sauce|Wine|Beer|NonAlcoholic|Starter|Dessert
    Suggestion   NVARCHAR(300) NOT NULL,
    Notes        NVARCHAR(300) NULL
);
GO

INSERT INTO IngredientPairing (DishKeywords, DishName, PairingType, Suggestion, Notes) VALUES
(N'beef stew,beef stew',N'Beef Stew',N'Bread',N'Crusty sourdough or a thick-sliced country loaf',N'Great for soaking up the broth'),
(N'beef stew,beef stew',N'Beef Stew',N'Starch',N'Creamy mashed potatoes or buttered egg noodles',NULL),
(N'beef stew,beef stew',N'Beef Stew',N'Vegetable',N'Roasted root vegetables (carrots, parsnips, turnips)',N'Complements the earthy stew flavors'),
(N'beef stew,beef stew',N'Beef Stew',N'Wine',N'Full-bodied red — Cabernet Sauvignon, Malbec, or Syrah',NULL),
(N'beef stew,beef stew',N'Beef Stew',N'Beer',N'Dark stout or porter',N'Can also be used in the stew itself'),
(N'beef stew,beef stew',N'Beef Stew',N'NonAlcoholic',N'Sparkling water with lemon, or warm apple cider',NULL),
(N'grilled salmon,salmon',N'Grilled Salmon',N'Vegetable',N'Asparagus, green beans, or roasted fennel',NULL),
(N'grilled salmon,salmon',N'Grilled Salmon',N'Starch',N'Wild rice pilaf or roasted baby potatoes',NULL),
(N'grilled salmon,salmon',N'Grilled Salmon',N'Sauce',N'Dill cream sauce, caper butter, or lemon beurre blanc',NULL),
(N'grilled salmon,salmon',N'Grilled Salmon',N'Wine',N'Crisp Pinot Gris, unoaked Chardonnay, or dry Rosé',NULL),
(N'grilled salmon,salmon',N'Grilled Salmon',N'Bread',N'Toasted sourdough or herb focaccia',NULL),
(N'pasta carbonara,carbonara',N'Pasta Carbonara',N'Vegetable',N'Wilted spinach, broccolini, or arugula salad',N'Cut the richness with greens'),
(N'pasta carbonara,carbonara',N'Pasta Carbonara',N'Bread',N'Garlic bread or ciabatta',NULL),
(N'pasta carbonara,carbonara',N'Pasta Carbonara',N'Wine',N'Pinot Grigio, Verdicchio, or Soave Classico',NULL),
(N'pasta carbonara,carbonara',N'Pasta Carbonara',N'Starter',N'Light Caesar salad or prosciutto with melon',NULL),
(N'roast chicken,whole chicken',N'Roast Chicken',N'Starch',N'Roasted potatoes, polenta, or crusty bread for pan juices',NULL),
(N'roast chicken,whole chicken',N'Roast Chicken',N'Vegetable',N'Roasted root veg, green beans almondine, or braised kale',NULL),
(N'roast chicken,whole chicken',N'Roast Chicken',N'Wine',N'Chardonnay (oaked) or light Pinot Noir',NULL),
(N'roast chicken,whole chicken',N'Roast Chicken',N'Sauce',N'Pan jus, herb gravy, or chimichurri',NULL),
(N'tacos,taco',N'Tacos',N'Vegetable',N'Elote (street corn), pickled jalapeños, or jicama slaw',NULL),
(N'tacos,taco',N'Tacos',N'Starch',N'Mexican rice and refried beans',N'Classic combination'),
(N'tacos,taco',N'Tacos',N'Beer',N'Mexican lager (Modelo, Pacifico) or a margarita',NULL),
(N'tacos,taco',N'Tacos',N'NonAlcoholic',N'Horchata or agua fresca',NULL),
(N'pizza,homemade pizza',N'Pizza',N'Vegetable',N'Simple arugula salad with lemon and parmesan',NULL),
(N'pizza,homemade pizza',N'Pizza',N'Beer',N'Italian Peroni, Moretti, or an IPA',NULL),
(N'pizza,homemade pizza',N'Pizza',N'Wine',N'Chianti, Barbera d''Asti, or a light Sangiovese',N'Italian wine with Italian food'),
(N'soup,chicken soup,vegetable soup',N'Soup',N'Bread',N'Crusty baguette, grilled cheese, or oyster crackers',NULL),
(N'soup,chicken soup,vegetable soup',N'Soup',N'Starter',N'Simple green salad',NULL),
(N'chili,beef chili',N'Chili',N'Bread',N'Cornbread — classic pairing',NULL),
(N'chili,beef chili',N'Chili',N'Starch',N'Tortilla chips, white rice, or baked potato',NULL),
(N'chili,beef chili',N'Chili',N'Beer',N'Amber ale, brown ale, or Mexican lager',NULL),
(N'grilled steak,steak',N'Grilled Steak',N'Vegetable',N'Grilled asparagus, creamed spinach, or Caesar salad',NULL),
(N'grilled steak,steak',N'Grilled Steak',N'Starch',N'Twice-baked potato, fries, or garlic mashed',NULL),
(N'grilled steak,steak',N'Grilled Steak',N'Wine',N'Cabernet Sauvignon, Malbec, or Bordeaux blend',NULL),
(N'grilled steak,steak',N'Grilled Steak',N'Sauce',N'Béarnaise, compound herb butter, or chimichurri',NULL);
GO
