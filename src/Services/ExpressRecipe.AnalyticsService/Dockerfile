FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared projects first
COPY ["src/ExpressRecipe.Client.Shared/ExpressRecipe.Client.Shared.csproj", "ExpressRecipe.Client.Shared/"]
COPY ["src/ExpressRecipe.Shared/ExpressRecipe.Shared.csproj", "ExpressRecipe.Shared/"]

# Copy Analytics Service project
COPY ["src/Services/ExpressRecipe.AnalyticsService/ExpressRecipe.AnalyticsService.csproj", "Services/ExpressRecipe.AnalyticsService/"]

# Restore dependencies
RUN dotnet restore "Services/ExpressRecipe.AnalyticsService/ExpressRecipe.AnalyticsService.csproj"

# Copy all source files
COPY src/ExpressRecipe.Client.Shared/. ExpressRecipe.Client.Shared/
COPY src/ExpressRecipe.Shared/. ExpressRecipe.Shared/
COPY src/Services/ExpressRecipe.AnalyticsService/. Services/ExpressRecipe.AnalyticsService/

# Build
WORKDIR "/src/Services/ExpressRecipe.AnalyticsService"
RUN dotnet build "ExpressRecipe.AnalyticsService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExpressRecipe.AnalyticsService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ExpressRecipe.AnalyticsService.dll"]
