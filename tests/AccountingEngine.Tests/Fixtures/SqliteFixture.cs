using AccountingEngine.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AccountingEngine.Tests.Fixtures;

/// <summary>
/// Self-contained file-based SQLite fixture (one temp .db file per test class
/// instance) for service-level tests. It validates the EF model and all service
/// logic without requiring Docker/Postgres. Per-class files eliminate cross-test
/// unique-key collisions that a shared in-memory database would cause.
/// </summary>
public sealed class SqliteFixture : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "ae-tests-" + Guid.NewGuid().ToString("N") + ".db");
    private readonly SqliteConnection _connection;

    public SqliteFixture()
    {
        _connection = new SqliteConnection("DataSource=" + _dbPath);
        _connection.Open();

        using var context = CreateDbContext();
        context.Database.EnsureCreated();
    }

    public string UniqueCode(string prefix) => (prefix + "_" + Guid.NewGuid().ToString("N").Substring(0, 16)).ToUpperInvariant();

    public AccountingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new AccountingDbContext(options);
    }

    public void Dispose()
    {
        _connection.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { /* best effort */ }
    }
}

