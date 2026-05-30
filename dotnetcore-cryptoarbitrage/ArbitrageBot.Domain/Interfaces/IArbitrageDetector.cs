using ArbitrageBot.Domain.Models;

namespace ArbitrageBot.Domain.Interfaces;

/// <summary>
/// Detecta oportunidades de arbitraje entre pares de exchanges.
/// </summary>
public interface IArbitrageDetector
{
    System.Threading.Channels.ChannelReader<ArbitrageOpportunity> GetReader();
}
