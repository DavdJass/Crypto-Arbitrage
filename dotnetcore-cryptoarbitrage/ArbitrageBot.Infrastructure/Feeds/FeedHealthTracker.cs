using System.Collections.Concurrent;

namespace ArbitrageBot.Infrastructure.Feeds;

/// <summary>
/// Monitorea el estado de conexión de cada feed WebSocket.
/// Thread-safe.
/// </summary>
public class FeedHealthTracker
{
    private readonly ConcurrentDictionary<string, FeedStatus> _statuses = new();

    /// <summary>Actualiza el estado de un feed.</summary>
    public void SetStatus(string exchangeId, string status, string details = "")
    {
        _statuses[exchangeId] = new FeedStatus(
            exchangeId,
            status,
            details,
            DateTime.UtcNow
        );
    }

    /// <summary>Obtiene el estado actual de un feed.</summary>
    public FeedStatus GetStatus(string exchangeId)
    {
        return _statuses.GetValueOrDefault(exchangeId, new FeedStatus(exchangeId, "unknown", "", DateTime.UtcNow));
    }

    /// <summary>Obtiene el estado de todos los feeds.</summary>
    public IReadOnlyDictionary<string, FeedStatus> GetAllStatuses()
    {
        return new Dictionary<string, FeedStatus>(_statuses);
    }
}

/// <summary>Estado de conexión de un feed.</summary>
/// <param name="ExchangeId">Nombre del exchange.</param>
/// <param name="Status">"connected", "connecting", "disconnected", "fallback_rest".</param>
/// <param name="Details">Mensaje adicional (error, latencia, etc).</param>
/// <param name="LastUpdated">Timestamp UTC de la última actualización.</param>
public record FeedStatus(
    string ExchangeId,
    string Status,
    string Details,
    DateTime LastUpdated
);
