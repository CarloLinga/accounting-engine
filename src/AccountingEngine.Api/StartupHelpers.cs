using Microsoft.AspNetCore.HttpOverrides;

namespace AccountingEngine.Api;

/// <summary>
/// Startup helpers for hosted environments (Render + Neon).
/// Resolves the Postgres connection string from Render env vars and
/// applies proxy/SSL fixes required behind Render's TLS edge proxy.
/// </summary>
public static class StartupHelpers
{
    public static void ConfigureForwardedHeaders(IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });
    }

    public static bool IsRunningOnRender()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RENDER"));

    public static string? ResolveConnectionString(ConfigurationManager configuration)
    {
        // 1. Standard .NET config (appsettings + env var ConnectionStrings__DefaultConnection)
        var fromConfig = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromConfig) && !IsLocalhostConnectionString(fromConfig))
            return EnsureSslForNeon(fromConfig);

        // 2. Explicit env vars (checked directly in case provider ordering differs)
        var directEnv =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(directEnv))
            return EnsureSslForNeon(directEnv.Trim());

        // 3. DATABASE_URL style: postgres://user:pass@host:port/db?sslmode=require
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return ConvertToNpgsqlConnectionString(databaseUrl.Trim());

        // 4. Local dev fallback (localhost string from appsettings).
        return string.IsNullOrWhiteSpace(fromConfig) ? null : fromConfig;
    }

    private static bool IsLocalhostConnectionString(string cs)
        => cs.Contains("localhost", StringComparison.OrdinalIgnoreCase)
        || cs.Contains("127.0.0.1")
        || cs.Contains("host=postgres", StringComparison.OrdinalIgnoreCase);

    private static string EnsureSslForNeon(string connectionString)
    {
        // Neon requires SSL. Force it when the user pasted a host without SslMode.
        if (connectionString.Contains("neon.tech", StringComparison.OrdinalIgnoreCase)
            && !connectionString.Contains("sslmode", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = connectionString.TrimEnd().TrimEnd(';') + ";SslMode=Require;";
        }
        return connectionString;
    }

    private static string ConvertToNpgsqlConnectionString(string databaseUrl)
    {
        // Already in Npgsql key=value format.
        if (databaseUrl.Contains("Host=", StringComparison.OrdinalIgnoreCase))
            return EnsureSslForNeon(databaseUrl);

        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.Trim('/');
        var sslMode = GetQueryValue(uri.Query, "sslmode") ?? "Require";

        return $"Host={uri.Host};Port={uri.Port};Database={database};Username={username};Password={password};SslMode={sslMode};";
    }

    private static string? GetQueryValue(string query, string key)
    {
        if (string.IsNullOrEmpty(query)) return null;
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Equals(key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }
}
