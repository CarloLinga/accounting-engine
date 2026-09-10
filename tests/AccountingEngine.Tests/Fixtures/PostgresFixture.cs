using System.Text.Json;
using AccountingEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace AccountingEngine.Tests.Fixtures;

/// <summary>
/// Starts a disposable PostgreSQL container for integration tests.
///
/// No real database credentials are stored in this repository. Credentials applied
/// to the (ephemeral) test container are resolved in priority order:
///   1. Environment variables: AE_TEST_DB_NAME, AE_TEST_DB_USERNAME, AE_TEST_DB_PASSWORD
///   2. The locally present, gitignored "src/AccountingEngine.Api/appsettings.json"
///      ("ConnectionStrings:DefaultConnection"), so developers reuse their own
///      local database credentials without committing them.
///   3. Safe non-secret defaults ("postgres"/"postgres"). These apply only to the
///      throwaway container inside the test run — never to real data.
/// </summary>
public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer;

    public PostgresFixture()
    {
        var (database, username, password) = ResolveTestCredentials();

        _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase(database)
            .WithUsername(username)
            .WithPassword(password)
            .Build();
    }

    public string ConnectionString => _dbContainer.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContainer.StopAsync();
    }

    public AccountingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AccountingDbContext(options);
    }

    private static (string Database, string Username, string Password) ResolveTestCredentials()
    {
        // 1. Environment variables (CI-friendly).
        var database = Environment.GetEnvironmentVariable("AE_TEST_DB_NAME");
        var username = Environment.GetEnvironmentVariable("AE_TEST_DB_USERNAME");
        var password = Environment.GetEnvironmentVariable("AE_TEST_DB_PASSWORD");

        // 2. Local (gitignored) API appsettings.json.
        if (string.IsNullOrWhiteSpace(database)
            && TryReadLocalApiConnectionString(out var local))
        {
            database = local.Database;
            username = local.Username;
            password = local.Password;
        }

        // 3. Ephemeral container defaults only; never real credentials.
        database = string.IsNullOrWhiteSpace(database) ? "postgres" : database;
        username = string.IsNullOrWhiteSpace(username) ? "postgres" : username;
        password = string.IsNullOrWhiteSpace(password) ? "postgres" : password;

        return (database, username, password);
    }

    /// <summary>
    /// Reads ConnectionStrings:DefaultConnection from the local, gitignored
    /// src/AccountingEngine.Api/appsettings.json so tests can reuse the
    /// developer's own database credentials without ever committing them.
    /// </summary>
    private static bool TryReadLocalApiConnectionString(out (string Database, string Username, string Password) credentials)
    {
        credentials = default;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AccountingEngine.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            return false;
        }

        var configPath = Path.Combine(directory.FullName, "src", "AccountingEngine.Api", "appsettings.json");
        if (!File.Exists(configPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(configPath));

            if (!document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
                || !connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection))
            {
                return false;
            }

            var builder = new NpgsqlConnectionStringBuilder(defaultConnection.GetString());
            credentials = (
                builder.Database ?? string.Empty,
                builder.Username ?? string.Empty,
                builder.Password ?? string.Empty);

            return !string.IsNullOrWhiteSpace(credentials.Database);
        }
        catch
        {
            // A missing/invalid local config must never break the test suite.
            return false;
        }
    }
}

// Ensure this CollectionDefinition is present in AccountingEngine.Tests
[CollectionDefinition("Database Collection")]
public class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
    // This class has no code; it is solely an anchor for xUnit collection wiring
}
