using System.Text.Json;
using System.Text.Json.Serialization;
using AccountingEngine.Application.Interfaces;
using AccountingEngine.Application.Services;
using AccountingEngine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore; // 1. Added Scalar namespace

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
builder.Services.AddOpenApi(); // .NET 9 native OpenAPI generator

var app = builder.Build();

// -----------------------------------------------------------------------------
// 4. HTTP Request Pipeline Configuration
// -----------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // Serves JSON schema at /openapi/v1.json
    app.MapScalarApiReference();    // 2. Replaced UseSwaggerUI with Scalar API UI
}

app.UseHttpsRedirection();

// Map Controller Routes (/api/accounts, /api/postings, etc.)
app.MapControllers();

app.Run();
