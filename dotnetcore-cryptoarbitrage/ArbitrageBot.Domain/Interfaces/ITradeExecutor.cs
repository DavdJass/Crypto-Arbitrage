using ArbitrageBot.Domain.Models;

namespace ArbitrageBot.Domain.Interfaces;

/// <summary>
/// Ejecuta trades simulados y persiste resultados.
/// </summary>
public interface ITradeExecutor
{
    System.Threading.Channels.ChannelReader<TradeResult> GetReader();
}
