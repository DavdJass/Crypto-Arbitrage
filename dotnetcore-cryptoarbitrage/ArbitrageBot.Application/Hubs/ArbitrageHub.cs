using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ArbitrageBot.Application.Hubs;

/// <summary>
/// SignalR Hub para push en tiempo real al frontend.
/// Eventos:
///   - OrderBookUpdated  (OrderBook)
///   - OpportunityFound   (ArbitrageOpportunity)
///   - TradeExecuted      (TradeResult)
/// </summary>
public class ArbitrageHub : Hub
{
    private readonly ILogger<ArbitrageHub> _logger;

    public ArbitrageHub(ILogger<ArbitrageHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Cliente conectado: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Cliente desconectado: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// El servidor puede llamar este método para enviar un OrderBook a todos los clientes.
    /// </summary>
    public async Task SendOrderBookUpdate(ArbitrageBot.Domain.Models.OrderBook orderBook)
    {
        await Clients.All.SendAsync("OrderBookUpdated", orderBook);
    }
}
