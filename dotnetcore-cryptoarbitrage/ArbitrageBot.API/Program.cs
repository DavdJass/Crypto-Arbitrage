using System.Reflection;
using ArbitrageBot.API;
using ArbitrageBot.API.Middleware;
using ArbitrageBot.Application;
using ArbitrageBot.Infrastructure;
using ArbitrageBot.Domain.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// ─── Opciones ───────────────────────────────────────────────
builder.Services.Configure<ExchangeOptions>(builder.Configuration.GetSection(ExchangeOptions.SectionName));
builder.Services.Configure<ArbitrageOptions>(builder.Configuration.GetSection(ArbitrageOptions.SectionName));
builder.Services.Configure<WalletOptions>(builder.Configuration.GetSection(WalletOptions.SectionName));
builder.Services.Configure<CircuitBreakerOptions>(builder.Configuration.GetSection(CircuitBreakerOptions.SectionName));
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SectionName));

// ─── Capas ─────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("Postgres") ?? "";
// SQLite: usar ruta absoluta para Railway (persistent volume)
var sqlitePath = Environment.GetEnvironmentVariable("SQLITE_PATH") ?? "arbitrage.db";

builder.Services.AddInfrastructureServices(connectionString, sqlitePath);
builder.Services.AddApplicationServices();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

// ─── Swagger ────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Crypto Arbitrage Bot API", Version = "v1",
        Description = "Sistema de detección y simulación de arbitraje BTC/USD en tiempo real."
    });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);

    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "Header: X-API-Key",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-API-Key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    { { new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        { Reference = new Microsoft.OpenApi.Models.OpenApiReference
            { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "ApiKey" } },
        Array.Empty<string>() } });
});

// ─── CORS ───────────────────────────────────────────────────
var secOpts = builder.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new();
var origins = (secOpts.AllowedOrigins ?? "*").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
builder.Services.AddCors(o =>
{
    o.AddPolicy("Api", p => {
        if (origins.Length == 1 && origins[0] == "*") p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        else p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
    o.AddPolicy("SignalR", p => {
        p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); app.UseSwaggerUI(c =>
    { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Crypto Arbitrage Bot v1"); c.RoutePrefix = "swagger"; });
}

app.UseCors("Api");
app.UseMiddleware<RateLimitMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ArbitrageBot.Application.Hubs.ArbitrageHub>("/hubs/arbitrage").RequireCors("SignalR");

await DatabaseMigrator.MigrateAsync(connectionString, app.Services.GetRequiredService<ILogger<Program>>());
app.Run();
