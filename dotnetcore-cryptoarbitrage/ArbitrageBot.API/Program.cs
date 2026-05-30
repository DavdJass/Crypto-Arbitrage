using System.Reflection;
using ArbitrageBot.API;
using ArbitrageBot.API.Middleware;
using ArbitrageBot.Application;
using ArbitrageBot.Infrastructure;
using ArbitrageBot.Domain.Configuration;

var builder = WebApplication.CreateBuilder(args);

// ─── Opciones desde appsettings.json ───────────────────────────
builder.Services.Configure<ExchangeOptions>(
    builder.Configuration.GetSection(ExchangeOptions.SectionName));
builder.Services.Configure<ArbitrageOptions>(
    builder.Configuration.GetSection(ArbitrageOptions.SectionName));
builder.Services.Configure<WalletOptions>(
    builder.Configuration.GetSection(WalletOptions.SectionName));
builder.Services.Configure<CircuitBreakerOptions>(
    builder.Configuration.GetSection(CircuitBreakerOptions.SectionName));
builder.Services.Configure<SecurityOptions>(
    builder.Configuration.GetSection(SecurityOptions.SectionName));

// ─── Capas ────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionString 'Postgres' no configurada");

builder.Services.AddInfrastructureServices(connectionString);
builder.Services.AddApplicationServices();

// ─── Controllers ───────────────────────────────────────────────
builder.Services.AddControllers();

// ─── Swagger con documentación XML ─────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Crypto Arbitrage Bot API",
        Version = "v1",
        Description = "Sistema de detección y simulación de arbitraje BTC/USD entre múltiples exchanges en tiempo real.",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "ArbitrageBot"
        }
    });

    // Incluir comentarios XML de los controllers
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);

    // Input de API Key en Swagger UI
    c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "API Key requerida. Header: X-API-Key",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Name = "X-API-Key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ─── CORS (producción: solo frontend autorizado) ──────────────
var securityOpts = builder.Configuration
    .GetSection(SecurityOptions.SectionName)
    .Get<SecurityOptions>() ?? new SecurityOptions();

var allowedOrigins = (securityOpts.AllowedOrigins ?? "*")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCors", policy =>
    {
        if (allowedOrigins.Length == 1 && allowedOrigins[0] == "*")
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });

    // SignalR necesita AllowCredentials + orígenes específicos
    options.AddPolicy("SignalR", policy =>
    {
        if (allowedOrigins.Length == 1 && allowedOrigins[0] == "*")
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

var app = builder.Build();

// ─── Pipeline HTTP ────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Crypto Arbitrage Bot v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("ApiCors");
app.UseMiddleware<RateLimitMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();
app.UseAuthorization();
app.MapControllers();

// ─── SignalR Hub con su propia política CORS ───────────────────
app.MapHub<ArbitrageBot.Application.Hubs.ArbitrageHub>("/hubs/arbitrage")
   .RequireCors("SignalR");

// ─── Auto-migración de base de datos (PostgreSQL) ────────────
await DatabaseMigrator.MigrateAsync(connectionString, app.Services.GetRequiredService<ILogger<Program>>());

app.Run();
