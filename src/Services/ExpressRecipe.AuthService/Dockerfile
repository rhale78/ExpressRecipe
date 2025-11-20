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

# Copy Auth Service project
COPY ["src/Services/ExpressRecipe.AuthService/ExpressRecipe.AuthService.csproj", "Services/ExpressRecipe.AuthService/"]

# Restore dependencies
RUN dotnet restore "Services/ExpressRecipe.AuthService/ExpressRecipe.AuthService.csproj"

# Copy all source files
COPY src/ExpressRecipe.Client.Shared/. ExpressRecipe.Client.Shared/
COPY src/ExpressRecipe.Shared/. ExpressRecipe.Shared/
COPY src/ExpressRecipe.Data.Common/. ExpressRecipe.Data.Common/
COPY src/Services/ExpressRecipe.AuthService/. Services/ExpressRecipe.AuthService/

# Build
WORKDIR "/src/Services/ExpressRecipe.AuthService"
RUN dotnet build "ExpressRecipe.AuthService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExpressRecipe.AuthService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ExpressRecipe.AuthService.dll"]
