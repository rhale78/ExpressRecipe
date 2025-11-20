#!/bin/bash

# Update CommunityService Program.cs
sed -i '/var connectionString = builder.Configuration.GetConnectionString("communitydb"/a\
\n// Register repositories\
builder.Services.AddScoped<ICommunityRepository>(sp =>\
    new CommunityRepository(connectionString, sp.GetRequiredService<ILogger<CommunityRepository>>()));' \
    src/Services/ExpressRecipe.CommunityService/Program.cs

# Update SyncService Program.cs
sed -i '/var connectionString = builder.Configuration.GetConnectionString("syncdb"/a\
\n// Register repositories\
builder.Services.AddScoped<ISyncRepository>(sp =>\
    new SyncRepository(connectionString, sp.GetRequiredService<ILogger<SyncRepository>>()));' \
    src/Services/ExpressRecipe.SyncService/Program.cs

# Update SearchService Program.cs
sed -i '/var connectionString = builder.Configuration.GetConnectionString("searchdb"/a\
\n// Register repositories\
builder.Services.AddScoped<ISearchRepository>(sp =>\
    new SearchRepository(connectionString, sp.GetRequiredService<ILogger<SearchRepository>>()));' \
    src/Services/ExpressRecipe.SearchService/Program.cs

# Update AnalyticsService Program.cs
sed -i '/var connectionString = builder.Configuration.GetConnectionString("analyticsdb"/a\
\n// Register repositories\
builder.Services.AddScoped<IAnalyticsRepository>(sp =>\
    new AnalyticsRepository(connectionString, sp.GetRequiredService<ILogger<AnalyticsRepository>>()));' \
    src/Services/ExpressRecipe.AnalyticsService/Program.cs

echo "Updated final 4 Program.cs files"
