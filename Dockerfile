# Step 1: Build stage using .NET 9 SDK
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["AccountingEngine.Api/AccountingEngine.Api.csproj", "AccountingEngine.Api/"]
COPY ["AccountingEngine.Infrastructure/AccountingEngine.Infrastructure.csproj", "AccountingEngine.Infrastructure/"]
# Add any additional project references here (e.g., Domain, Core, Application)
RUN dotnet restore "AccountingEngine.Api/AccountingEngine.Api.csproj"

# Copy full source code and build release
COPY . .
WORKDIR "/src/AccountingEngine.Api"
RUN dotnet publish "AccountingEngine.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Step 2: Runtime stage using lightweight ASP.NET runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AccountingEngine.Api.dll"]
