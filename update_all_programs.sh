#!/bin/bash

# Update ShoppingService Program.cs
sed -i '/var connectionString = builder.Configuration.GetConnectionString("shoppingdb"/a\
\n// Register repositories\
builder.Services.AddScoped<IShoppingRepository>(sp =>\
    new ShoppingRepository(connectionString, sp.GetRequiredService<ILogger<ShoppingRepository>>()));' \
    src/Services/ExpressRecipe.ShoppingService/Program.cs

# Update NotificationService Program.cs
sed -i '/var connectionString = builder.Configuration.GetConnectionString("notificationdb"/a\
\n// Register repositories\
builder.Services.AddScoped<INotificationRepository>(sp =>\
    new NotificationRepository(connectionString, sp.GetRequiredService<ILogger<NotificationRepository>>()));' \
    src/Services/ExpressRecipe.NotificationService/Program.cs

# Update MealPlanningService Program.cs
sed -i '/var connectionString = builder.Configuration.GetConnectionString("mealplandb"/a\
\n// Register repositories\
builder.Services.AddScoped<IMealPlanningRepository>(sp =>\
    new MealPlanningRepository(connectionString, sp.GetRequiredService<ILogger<MealPlanningRepository>>()));' \
    src/Services/ExpressRecipe.MealPlanningService/Program.cs

# Update PriceService Program.cs
sed -i '/var connectionString = builder.Configuration.GetConnectionString("pricedb"/a\
\n// Register repositories\
builder.Services.AddScoped<IPriceRepository>(sp =>\
    new PriceRepository(connectionString, sp.GetRequiredService<ILogger<PriceRepository>>()));' \
    src/Services/ExpressRecipe.PriceService/Program.cs

# Update RecallService Program.cs
sed -i '/var connectionString = builder.Configuration.GetConnectionString("recalldb"/a\
\n// Register repositories\
builder.Services.AddScoped<IRecallRepository>(sp =>\
    new RecallRepository(connectionString, sp.GetRequiredService<ILogger<RecallRepository>>()));' \
    src/Services/ExpressRecipe.RecallService/Program.cs

echo "Updated all Program.cs files"
