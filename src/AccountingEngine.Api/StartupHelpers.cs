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
        // NOTE: .NET already merges env vars into `configuration`, so
        // ConnectionStrings__DefaultConnection set on Render normally arrives
        // via fromConfig. The direct Environment reads below are belt-and-braces.
        var fromConfig = configuration.GetConnectionString("DefaultConnection");

        // 1. Explicit non-localhost config wins (Render dashboard value, whether
        //    it arrives via the config provider or the raw env var).
        var directEnv =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DefaultConnection");
        var effective = !string.IsNullOrWhiteSpace(fromConfig) && !IsLocalhostConnectionString(fromConfig)
            ? fromConfig
            : directEnv;
        if (!string.IsNullOrWhiteSpace(effective))
            return EnsureSslForNeon(Sanitize(effective.Trim()));

        // 2. Local dev wins over a stale machine-level DATABASE_URL: an explicit
        //    localhost string in appsettings means "I am developing locally" —
        //    never let a leftover DATABASE_URL from another project (e.g. ezpos)
        //    hijack local runs or force SSL against a non-SSL local Postgres.
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return Sanitize(fromConfig);

        // 3. DATABASE_URL style: postgres://user:pass@host:port/db?sslmode=require
        //    (only when nothing else is configured).
        var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (!string.IsNullOrWhiteSpace(databaseUrl))
            return ConvertToNpgsqlConnectionString(Sanitize(databaseUrl.Trim()));

        // 4. Nothing configured.
        return null;
    }

    /// <summary>
    /// Strips stray wrapping quotes pasted from dashboards, e.g. '"Host=...;..."'.
    /// This was the live Render 500: a leading quote turned Host into '"Host',
    /// which Npgsql rejects with KeyNotFoundException on '"host'.
    /// </summary>
    private static string Sanitize(string cs)
    {
        cs = cs.Trim();
        if ((cs.StartsWith('"') && cs.EndsWith('"')) || (cs.StartsWith('\'') && cs.EndsWith('\'')))
            cs = cs[1..^1].Trim();
        return cs.Trim('"', '\'').Trim();
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

    /// <summary>Which configuration source the connection string came from (no secrets).</summary>
    public static string GetConnectionStringSource(ConfigurationManager configuration)
    {
        var fromConfig = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromConfig) && !IsLocalhostConnectionString(fromConfig))
            return "ConnectionStrings:DefaultConnection (non-localhost)";
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings:DefaultConnection"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DefaultConnection")))
            return "environment variable (ConnectionStrings__DefaultConnection)";
        if (!string.IsNullOrWhiteSpace(fromConfig))
            return "appsettings.json (localhost)";
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATABASE_URL")))
            return "environment variable (DATABASE_URL)";
        return "none found";
    }

    /// <summary>Redacted connection info safe to return from a health endpoint (no password).</summary>
    public static Dictionary<string, string> GetSafeConnectionInfo(string? connectionString)
    {
        var info = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(connectionString))
            return info;
        if (connectionString.TrimStart().StartsWith("postgres", StringComparison.OrdinalIgnoreCase))
        {
            info["format"] = "url (postgres://...)";
            try
            {
                var uri = new Uri(connectionString.Trim());
                info["host"] = uri.Host;
                info["port"] = uri.Port.ToString();
                info["database"] = uri.AbsolutePath.Trim('/');
                info["username"] = uri.UserInfo.Split(':')[0];
            }
            catch { info["parse"] = "failed"; }
            return info;
        }
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            var key = kv[0].Trim();
            if (key.Equals("Password", StringComparison.OrdinalIgnoreCase)) continue;
            info[key] = kv[1].Trim();
        }
        return info;
    }
}
