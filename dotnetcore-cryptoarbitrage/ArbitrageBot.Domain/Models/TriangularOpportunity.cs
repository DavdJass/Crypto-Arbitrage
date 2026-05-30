namespace ArbitrageBot.Domain.Models;

/// <summary>
/// Oportunidad de arbitraje triangular detectada dentro de un mismo exchange.
/// Ruta: BTC → USDT → ETH → BTC (u otras combinaciones).
/// </summary>
public record TriangularOpportunity(
    string ExchangeId,
    string Path,              // "BTC→USDT→ETH→BTC"
    decimal StartAmountBtc,
    decimal EndAmountBtc,
    decimal NetProfitBtc,
    decimal ReturnPct,
    decimal[] StepRates,      // [btcUsdtBid, ethUsdtAsk, ethBtcBid]
    DateTime DetectedAt
);
