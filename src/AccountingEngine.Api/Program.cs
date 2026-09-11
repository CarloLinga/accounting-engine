using System.Text.Json;
using System.Text.Json.Serialization;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Application.Services;
using AccountingEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Xml.XPath;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------------
// 1. Database Configuration (EF Core + Npgsql PostgreSQL)
// -----------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found in appsettings.json.");

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
builder.Services.AddScoped<IJournalService, JournalService>();
builder.Services.AddScoped<ISourceRuleService, SourceRuleService>();
builder.Services.AddScoped<ITrialBalanceService, TrialBalanceService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
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

app.UseHttpsRedirection();
app.UseAuthorization();
// Map Controller Routes (/api/accounts, /api/postings, etc.)
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    name = "Accounting Engine API",
    status = "healthy",
    api = "/api"
}));

app.Run();
