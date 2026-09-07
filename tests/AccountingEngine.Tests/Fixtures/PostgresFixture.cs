using AccountingEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace AccountingEngine.Tests.Fixtures;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("AccountingDB")
        .WithUsername("postgres")
        .WithPassword("pSam1230s")
        .Build();

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
}

// Ensure this CollectionDefinition is present in AccountingEngine.Tests
[CollectionDefinition("Database Collection")]
public class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
    // This class has no code; it is solely an anchor for xUnit collection wiring
}
