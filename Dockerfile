# Step 1: Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files first so dependency restore can be cached
COPY ["src/AccountingEngine.Api/AccountingEngine.Api.csproj", "src/AccountingEngine.Api/"]
COPY ["src/AccountingEngine.Application/AccountingEngine.Application.csproj", "src/AccountingEngine.Application/"]
COPY ["src/AccountingEngine.Core/AccountingEngine.Core.csproj", "src/AccountingEngine.Core/"]
COPY ["src/AccountingEngine.Infrastructure/AccountingEngine.Infrastructure.csproj", "src/AccountingEngine.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/AccountingEngine.Api/AccountingEngine.Api.csproj"

# Copy remaining source code
COPY . .

# Build and Publish
WORKDIR "/src/src/AccountingEngine.Api"
RUN dotnet publish "AccountingEngine.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Step 2: Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AccountingEngine.Api.dll"]
