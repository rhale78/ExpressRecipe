FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared projects first
COPY ["src/ExpressRecipe.Client.Shared/ExpressRecipe.Client.Shared.csproj", "ExpressRecipe.Client.Shared/"]
COPY ["src/ExpressRecipe.Shared/ExpressRecipe.Shared.csproj", "ExpressRecipe.Shared/"]

# Copy Price Service project
COPY ["src/Services/ExpressRecipe.PriceService/ExpressRecipe.PriceService.csproj", "Services/ExpressRecipe.PriceService/"]

# Restore dependencies
RUN dotnet restore "Services/ExpressRecipe.PriceService/ExpressRecipe.PriceService.csproj"

# Copy all source files
COPY src/ExpressRecipe.Client.Shared/. ExpressRecipe.Client.Shared/
COPY src/ExpressRecipe.Shared/. ExpressRecipe.Shared/
COPY src/Services/ExpressRecipe.PriceService/. Services/ExpressRecipe.PriceService/

# Build
WORKDIR "/src/Services/ExpressRecipe.PriceService"
RUN dotnet build "ExpressRecipe.PriceService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExpressRecipe.PriceService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ExpressRecipe.PriceService.dll"]
