FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy shared projects first
COPY ["src/ExpressRecipe.Client.Shared/ExpressRecipe.Client.Shared.csproj", "ExpressRecipe.Client.Shared/"]
COPY ["src/ExpressRecipe.Shared/ExpressRecipe.Shared.csproj", "ExpressRecipe.Shared/"]

# Copy Blazor Web project
COPY ["src/Frontends/ExpressRecipe.BlazorWeb/ExpressRecipe.BlazorWeb.csproj", "Frontends/ExpressRecipe.BlazorWeb/"]

# Restore dependencies
RUN dotnet restore "Frontends/ExpressRecipe.BlazorWeb/ExpressRecipe.BlazorWeb.csproj"

# Copy all source files
COPY src/ExpressRecipe.Client.Shared/. ExpressRecipe.Client.Shared/
COPY src/ExpressRecipe.Shared/. ExpressRecipe.Shared/
COPY src/Frontends/ExpressRecipe.BlazorWeb/. Frontends/ExpressRecipe.BlazorWeb/

# Build
WORKDIR "/src/Frontends/ExpressRecipe.BlazorWeb"
RUN dotnet build "ExpressRecipe.BlazorWeb.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ExpressRecipe.BlazorWeb.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ExpressRecipe.BlazorWeb.dll"]
