export interface OrderBook {
  exchangeId: string;
  bestBid: number;
  bestAsk: number;
  bidVolume: number;
  askVolume: number;
  spread?: number;
  timestamp: string;
  age?: string;
}
export interface ArbitrageOpportunity {
  buyExchange: string;
  sellExchange: string;
  askPrice: number;
  bidPrice: number;
  volume: number;
  netProfit: number;
  returnPct: number;
  detectedAt: string;
}
export interface TradeResult {
  id: string;
  buyExchange: string;
  sellExchange: string;
  volume: number;
  netProfit: number;
  returnPct: number;
  isProfit: boolean;
  status: string;
  executedAt: string;
}
export interface WalletBalance {
  exchangeId: string;
  usdtBalance: number;
  btcBalance: number;
}
export interface TradeSummary {
  totalPnl: number;
  totalTrades: number;
  winningTrades: number;
  winRate: number;
}
export interface CircuitBreakerState {
  isOpen: boolean;
  openedAt: string | null;
  closedAt: string | null;
  consecutiveLosses: number;
  recentTradesCount: number;
}
export interface FeedStatus {
  exchangeId: string;
  status: string;
  details: string;
  lastUpdated: string;
  age?: string;
  avgLatencyMs?: number;
}
