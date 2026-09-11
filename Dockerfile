# Step 1: Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files using exact relative paths from root
COPY ["AccountingEngine.Api/AccountingEngine.Api.csproj", "AccountingEngine.Api/"]
COPY ["AccountingEngine.Infrastructure/AccountingEngine.Infrastructure.csproj", "AccountingEngine.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "AccountingEngine.Api/AccountingEngine.Api.csproj"

# Copy remaining source code
COPY . .

# Build and Publish
WORKDIR "/src/AccountingEngine.Api"
RUN dotnet publish "AccountingEngine.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Step 2: Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AccountingEngine.Api.dll"]
