FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy shared projects first
COPY ["src/ExpressRecipe.ServiceDefaults/ExpressRecipe.ServiceDefaults.csproj", "ExpressRecipe.ServiceDefaults/"]
COPY ["src/ExpressRecipe.Shared/ExpressRecipe.Shared.csproj", "ExpressRecipe.Shared/"]
COPY ["src/ExpressRecipe.Data.Common/ExpressRecipe.Data.Common.csproj", "ExpressRecipe.Data.Common/"]

# Copy RestaurantService project
COPY ["src/Services/ExpressRecipe.RestaurantService/ExpressRecipe.RestaurantService.csproj", "Services/ExpressRecipe.RestaurantService/"]

# Restore dependencies
RUN dotnet restore "Services/ExpressRecipe.RestaurantService/ExpressRecipe.RestaurantService.csproj"

# Copy all source files
COPY src/ExpressRecipe.ServiceDefaults/. ExpressRecipe.ServiceDefaults/
COPY src/ExpressRecipe.Shared/. ExpressRecipe.Shared/
COPY src/ExpressRecipe.Data.Common/. ExpressRecipe.Data.Common/
COPY src/Services/ExpressRecipe.RestaurantService/. Services/ExpressRecipe.RestaurantService/

# Build
WORKDIR "/src/Services/ExpressRecipe.RestaurantService"
RUN dotnet build "ExpressRecipe.RestaurantService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExpressRecipe.RestaurantService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ExpressRecipe.RestaurantService.dll"]
