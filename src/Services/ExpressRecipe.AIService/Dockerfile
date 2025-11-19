FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared projects first
COPY ["src/ExpressRecipe.Client.Shared/ExpressRecipe.Client.Shared.csproj", "ExpressRecipe.Client.Shared/"]

# Copy AI Service project
COPY ["src/Services/ExpressRecipe.AIService/ExpressRecipe.AIService.csproj", "Services/ExpressRecipe.AIService/"]

# Restore dependencies
RUN dotnet restore "Services/ExpressRecipe.AIService/ExpressRecipe.AIService.csproj"

# Copy all source files
COPY src/ExpressRecipe.Client.Shared/. ExpressRecipe.Client.Shared/
COPY src/Services/ExpressRecipe.AIService/. Services/ExpressRecipe.AIService/

# Build
WORKDIR "/src/Services/ExpressRecipe.AIService"
RUN dotnet build "ExpressRecipe.AIService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExpressRecipe.AIService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ExpressRecipe.AIService.dll"]
