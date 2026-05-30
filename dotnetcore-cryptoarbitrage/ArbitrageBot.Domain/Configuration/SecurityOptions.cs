namespace ArbitrageBot.Domain.Configuration;

/// <summary>
/// Configuración de seguridad de la API.
/// </summary>
public class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>API Key requerida en header X-API-Key para acceder a los endpoints REST.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Orígenes permitidos para CORS (separados por coma).</summary>
    public string AllowedOrigins { get; set; } = string.Empty;

    /// <summary>Límite de requests por minuto por IP.</summary>
    public int RateLimitPerMinute { get; set; } = 60;
}
