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
('lumpy gravy,lumps gravy,gravy lumps,lumpy sauce',
 'Lumpy Gravy/Sauce',
 'Starch (cornstarch/flour) added dry to hot liquid causes it to clump instantly.',
 'Always mix starch with equal parts COLD water or broth first to form a slurry, then whisk into the hot liquid slowly.'),
('too salty,over salted,too much salt,salty soup,salty stew',
 'Over-Salted Dish',
 'Too much salt was added during cooking or the recipe was followed too closely without tasting.',
 'Options: 1) Add peeled raw potato chunks and simmer 15 min (absorb salt), then remove. 2) Add acid — lemon juice or white vinegar brightens and balances. 3) Dilute with unsalted broth or water and adjust other seasonings. 4) Add a starch (rice, pasta) to absorb.'),
('bread didnt rise,bread not rising,yeast not working,dense bread,flat bread',
 'Bread Didn''t Rise',
 'Yeast killed by water that was too hot (above 115°F), yeast was old/expired, or dough was too cold.',
 'Proof yeast first: dissolve in 105–110°F water with a pinch of sugar; it should foam in 5–10 min. If it doesn''t, replace the yeast. Keep dough somewhere warm (80°F) to rise.'),
('rubbery eggs,tough eggs,overcooked eggs,chewy eggs',
 'Rubbery Scrambled/Fried Eggs',
 'High heat causes proteins to tighten and expel moisture rapidly.',
 'Use low-medium heat. For scrambled eggs, remove from heat while still slightly wet — carry-over heat finishes them. Add a splash of cream or water for fluffier texture.'),
('cake sank,cake collapsed,sunken cake,cake dip middle',
 'Cake Sank in the Middle',
 'Underbaked, oven door opened too early (cold air collapses structure), or too much leavening.',
 'Don''t open the oven door in the first 2/3 of bake time. Test with a toothpick — must come out clean. Check oven temperature accuracy with a thermometer.'),
('tough steak,chewy steak,steak too chewy,rubbery steak',
 'Tough/Chewy Steak',
 'Cut with the grain (long muscle fibers intact) or steak was a tougher cut cooked wrong.',
 'Always slice AGAINST the grain — perpendicular to the muscle fiber lines. For tougher cuts (chuck, flank), use low-and-slow braising instead of high-heat grilling.'),
('bitter coffee,bitter espresso,over extracted',
 'Bitter Coffee/Espresso',
 'Over-extraction from too fine a grind, too long brew time, or stale beans.',
 'Grind coarser, reduce brew time, or use fresher beans (roasted within 2–4 weeks). For espresso, check your 25–30 second extraction window.'),
('greasy fried,soggy fried,fried food not crispy,oil soaked',
 'Greasy/Soggy Fried Food',
 'Oil temperature too low; food added while oil is cold causes absorption instead of crisping.',
 'Heat oil to 350–375°F before adding food. Don''t overcrowd the pan — it drops oil temperature. Use a thermometer. Let food drain on a wire rack, not paper towels (steam makes things soggy).'),
('watery sauce,thin sauce,sauce too thin,sauce not thickening',
 'Thin/Watery Sauce',
 'Not enough reduction time, or lid left on (trapping steam that would evaporate).',
 'Simmer UNCOVERED over medium heat until desired consistency. Add a cornstarch slurry (1 tsp + 1 tsp cold water) for a quick thickener. Reduce heat-and-time before adding dairy.'),
('pasta mushy,overcooked pasta,soft pasta,pasta too soft',
 'Overcooked Pasta',
 'Cooked past al dente, or left sitting in hot water after draining.',
 'Cook 1–2 minutes LESS than package time — pasta continues cooking in hot sauce. Drain and transfer directly to sauce immediately. Never rinse pasta (removes starch needed for sauce adhesion).'),
('dense meatballs,tough meatballs,hard meatballs',
 'Dense/Tough Meatballs',
 'Over-mixing develops gluten in the meat, making them rubbery.',
 'Mix just until combined — stop the moment no dry patches remain. Use a light hand. Adding soaked breadcrumbs or ricotta also keeps them tender.'),
('clumped rice,sticky rice,mushy rice,gummy rice',
 'Clumped/Mushy Rice',
 'Stirred during cooking (releases starch), too much water, or wrong ratio.',
 'Don''t stir once boiling. Use exact 1:2 ratio (rice:water) for long grain. Use tight-fitting lid. Let rest 5 min off heat before fluffing.'),
('burnt garlic,bitter garlic,garlic too dark',
 'Burnt/Bitter Garlic',
 'Garlic burns in seconds over high heat and turns bitter.',
 'Add garlic over medium heat, not high. Sauté 30–60 seconds until fragrant — the moment you smell it, it''s almost done. If it browns, start over; burnt garlic cannot be saved.'),
('fish sticking,sticking pan,food sticking',
 'Food Sticking to Pan',
 'Pan or oil not hot enough before adding food, or food moved too soon.',
 'Heat the pan first, then add oil, then add food. Food naturally releases when a crust forms — wait for it to release freely before flipping. Pat protein dry before cooking.'),
('flat cookies,spread cookies,cookies too thin',
 'Cookies Spread Too Much',
 'Butter too warm/melted, too much sugar, or no chilling.',
 'Use room-temperature (not melted) butter. Chill dough 30–60 min before baking. Check flour measurement (spoon into cup, level off — don''t pack). Slightly underbake; they firm up cooling.');
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
('beef stew,beef stew','Beef Stew','Bread','Crusty sourdough or a thick-sliced country loaf','Great for soaking up the broth'),
('beef stew,beef stew','Beef Stew','Starch','Creamy mashed potatoes or buttered egg noodles',NULL),
('beef stew,beef stew','Beef Stew','Vegetable','Roasted root vegetables (carrots, parsnips, turnips)','Complements the earthy stew flavors'),
('beef stew,beef stew','Beef Stew','Wine','Full-bodied red — Cabernet Sauvignon, Malbec, or Syrah',NULL),
('beef stew,beef stew','Beef Stew','Beer','Dark stout or porter','Can also be used in the stew itself'),
('beef stew,beef stew','Beef Stew','NonAlcoholic','Sparkling water with lemon, or warm apple cider',NULL),
('grilled salmon,salmon','Grilled Salmon','Vegetable','Asparagus, green beans, or roasted fennel',NULL),
('grilled salmon,salmon','Grilled Salmon','Starch','Wild rice pilaf or roasted baby potatoes',NULL),
('grilled salmon,salmon','Grilled Salmon','Sauce','Dill cream sauce, caper butter, or lemon beurre blanc',NULL),
('grilled salmon,salmon','Grilled Salmon','Wine','Crisp Pinot Gris, unoaked Chardonnay, or dry Rosé',NULL),
('grilled salmon,salmon','Grilled Salmon','Bread','Toasted sourdough or herb focaccia',NULL),
('pasta carbonara,carbonara','Pasta Carbonara','Vegetable','Wilted spinach, broccolini, or arugula salad','Cut the richness with greens'),
('pasta carbonara,carbonara','Pasta Carbonara','Bread','Garlic bread or ciabatta',NULL),
('pasta carbonara,carbonara','Pasta Carbonara','Wine','Pinot Grigio, Verdicchio, or Soave Classico',NULL),
('pasta carbonara,carbonara','Pasta Carbonara','Starter','Light Caesar salad or prosciutto with melon',NULL),
('roast chicken,whole chicken','Roast Chicken','Starch','Roasted potatoes, polenta, or crusty bread for pan juices',NULL),
('roast chicken,whole chicken','Roast Chicken','Vegetable','Roasted root veg, green beans almondine, or braised kale',NULL),
('roast chicken,whole chicken','Roast Chicken','Wine','Chardonnay (oaked) or light Pinot Noir',NULL),
('roast chicken,whole chicken','Roast Chicken','Sauce','Pan jus, herb gravy, or chimichurri',NULL),
('tacos,taco','Tacos','Vegetable','Elote (street corn), pickled jalapeños, or jicama slaw',NULL),
('tacos,taco','Tacos','Starch','Mexican rice and refried beans','Classic combination'),
('tacos,taco','Tacos','Beer','Mexican lager (Modelo, Pacifico) or a margarita',NULL),
('tacos,taco','Tacos','NonAlcoholic','Horchata or agua fresca',NULL),
('pizza,homemade pizza','Pizza','Vegetable','Simple arugula salad with lemon and parmesan',NULL),
('pizza,homemade pizza','Pizza','Beer','Italian Peroni, Moretti, or an IPA',NULL),
('pizza,homemade pizza','Pizza','Wine','Chianti, Barbera d''Asti, or a light Sangiovese','Italian wine with Italian food'),
('soup,chicken soup,vegetable soup','Soup','Bread','Crusty baguette, grilled cheese, or oyster crackers',NULL),
('soup,chicken soup,vegetable soup','Soup','Starter','Simple green salad',NULL),
('chili,beef chili','Chili','Bread','Cornbread — classic pairing',NULL),
('chili,beef chili','Chili','Starch','Tortilla chips, white rice, or baked potato',NULL),
('chili,beef chili','Chili','Beer','Amber ale, brown ale, or Mexican lager',NULL),
('grilled steak,steak','Grilled Steak','Vegetable','Grilled asparagus, creamed spinach, or Caesar salad',NULL),
('grilled steak,steak','Grilled Steak','Starch','Twice-baked potato, fries, or garlic mashed',NULL),
('grilled steak,steak','Grilled Steak','Wine','Cabernet Sauvignon, Malbec, or Bordeaux blend',NULL),
('grilled steak,steak','Grilled Steak','Sauce','Béarnaise, compound herb butter, or chimichurri',NULL);
GO
