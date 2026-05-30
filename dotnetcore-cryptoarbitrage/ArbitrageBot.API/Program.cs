using System.Reflection;
using ArbitrageBot.API;
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
});

// ─── CORS (para frontend en Vercel) ────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("SignalR", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
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

app.UseCors("SignalR");
app.UseAuthorization();
app.MapControllers();

// ─── SignalR Hub ───────────────────────────────────────────────
app.MapHub<ArbitrageBot.Application.Hubs.ArbitrageHub>("/hubs/arbitrage");

// ─── Auto-migración de base de datos (PostgreSQL) ────────────
await DatabaseMigrator.MigrateAsync(connectionString, app.Services.GetRequiredService<ILogger<Program>>());

app.Run();
