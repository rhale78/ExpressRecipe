FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared projects first
COPY ["src/ExpressRecipe.Client.Shared/ExpressRecipe.Client.Shared.csproj", "ExpressRecipe.Client.Shared/"]
COPY ["src/ExpressRecipe.Shared/ExpressRecipe.Shared.csproj", "ExpressRecipe.Shared/"]
COPY ["src/ExpressRecipe.Data.Common/ExpressRecipe.Data.Common.csproj", "ExpressRecipe.Data.Common/"]

# Copy MealPlanning Service project
COPY ["src/Services/ExpressRecipe.MealPlanningService/ExpressRecipe.MealPlanningService.csproj", "Services/ExpressRecipe.MealPlanningService/"]

# Restore dependencies
RUN dotnet restore "Services/ExpressRecipe.MealPlanningService/ExpressRecipe.MealPlanningService.csproj"

# Copy all source files
COPY src/ExpressRecipe.Client.Shared/. ExpressRecipe.Client.Shared/
COPY src/ExpressRecipe.Shared/. ExpressRecipe.Shared/
COPY src/ExpressRecipe.Data.Common/. ExpressRecipe.Data.Common/
COPY src/Services/ExpressRecipe.MealPlanningService/. Services/ExpressRecipe.MealPlanningService/

# Build
WORKDIR "/src/Services/ExpressRecipe.MealPlanningService"
RUN dotnet build "ExpressRecipe.MealPlanningService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExpressRecipe.MealPlanningService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ExpressRecipe.MealPlanningService.dll"]
