using System.Text.Json;
using System.Text.Json.Serialization;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Application.Services;
using AccountingEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Xml.XPath;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;

using AccountingEngine.Api;

var builder = WebApplication.CreateBuilder(args);

StartupHelpers.ConfigureForwardedHeaders(builder.Services);

// CORS: financial statements are public read-only data; allow browser front-ends
// (Excel Power Query, Google Apps Script, and direct JS fetch) to call the API.
const string CorsPolicyName = "Frontend";
builder.Services.AddCors(options => options.AddPolicy(CorsPolicyName, policy =>
    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// -----------------------------------------------------------------------------
// 1. Database Configuration (EF Core + Npgsql PostgreSQL)
// -----------------------------------------------------------------------------
var connectionString = StartupHelpers.ResolveConnectionString(builder.Configuration)
    ?? throw new InvalidOperationException(
        "No database connection string found. Set 'ConnectionStrings__DefaultConnection' (or DATABASE_URL) in the Render environment variables to your Neon Postgres connection string.");

builder.Services.AddDbContext<AccountingDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsAssembly(typeof(AccountingDbContext).Assembly.FullName);
    });

    options.UseSnakeCaseNamingConvention();
});
builder.Services.AddScoped<IAccountingDbContext>(sp => sp.GetRequiredService<AccountingDbContext>());

// -----------------------------------------------------------------------------
// 2. Application Services Dependency Injection
// -----------------------------------------------------------------------------
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<ISalesJournalService, SalesJournalService>();
builder.Services.AddScoped<IJournalService, JournalService>();
builder.Services.AddScoped<ISourceRuleService, SourceRuleService>();
builder.Services.AddScoped<ITrialBalanceService, TrialBalanceService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IAccountBalanceProvider, AccountBalanceProvider>();
builder.Services.AddScoped<IFinancialStatementService, FinancialStatementService>();
// builder.Services.AddScoped<IPostingEngine, PostingEngine>(); // Register when built

// -----------------------------------------------------------------------------
// 3. API Framework Services (Controllers & OpenAPI)
// -----------------------------------------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // 1. Allow case-insensitive property matching (camelCase -> PascalCase)
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        
        // 2. Output standard camelCase JSON responses
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        
        // 3. Serialize/Deserialize Enums as strings ("Asset" instead of 1)
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

// Configure OpenAPI for .NET 9 with XML Comments
builder.Services.AddOpenApi(options =>
{
    // Locate the compiled XML documentation file
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
    {
        var xmlDoc = new XPathDocument(xmlPath);
        var navigator = xmlDoc.CreateNavigator();

        // Operation transformer to extract <summary> and <param> tags for endpoints
        options.AddOperationTransformer((operation, context, cancellationToken) =>
        {
            var methodInfo = context.Description.ActionDescriptor.EndpointMetadata
                .OfType<MethodInfo>()
                .FirstOrDefault();

            if (methodInfo != null)
            {
                // Format C# method signature into XML member key (e.g. M:Namespace.Controller.Action)
                var memberKey = $"M:{methodInfo.DeclaringType?.FullName}.{methodInfo.Name}";
                var memberNode = navigator.SelectSingleNode($"/doc/members/member[@name='{memberKey}']");

                if (memberNode != null)
                {
                    var summaryNode = memberNode.SelectSingleNode("summary");
                    if (summaryNode != null)
                    {
                        operation.Summary = summaryNode.Value.Trim();
                    }
                }
            }

            return Task.CompletedTask;
        });
    }

    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "Accounting Engine API";
        document.Info.Version = "v0.1";
        document.Info.Description = "Professional headless Clean Architecture accounting engine.";
        return Task.CompletedTask;
    });
});

var app = builder.Build();

// -----------------------------------------------------------------------------
// 4. HTTP Request Pipeline Configuration
// -----------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // Serves JSON schema at /openapi/v1.json
    // Map Scalar UI endpoint at /scalar/v1
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("Accounting Engine API Docs")
               .WithTheme(ScalarTheme.Purple)
               .WithDefaultHttpClient(ScalarTarget.JavaScript, ScalarClient.Axios);
    });
}

app.UseForwardedHeaders();
app.UseCors(CorsPolicyName);

if (!StartupHelpers.IsRunningOnRender())
{
    app.UseHttpsRedirection();
}
app.UseAuthorization();
// Map Controller Routes (/api/accounts, /api/postings, etc.)
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    name = "Accounting Engine API",
    status = "healthy",
    api = "/api"
}));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/health/db", async (AccountingDbContext db, ILogger<Program> logger) =>
{
    // NOTE: DbConnection.OpenAsync surfaces the REAL Npgsql error
    // (CanConnectAsync swallows it and just returns false).
    try
    {
        await using var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        var accountCount = await db.Accounts.CountAsync();
        return Results.Ok(new { status = "healthy", accounts = accountCount });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database health check failed.");
        return Results.Problem(detail: ex.ToString(), title: "Database connection failed", statusCode: 500);
    }
});

// TEMPORARY diagnostics: shows WHERE the connection string came from + redacted
// host/db/user (never the password). Remove once /health/db is green.
app.MapGet("/health/config", (IConfiguration config) => Results.Ok(new
{
    source = StartupHelpers.GetConnectionStringSource((ConfigurationManager)config),
    isRender = StartupHelpers.IsRunningOnRender(),
    connection = StartupHelpers.GetSafeConnectionInfo(connectionString)
}));

app.Run();
