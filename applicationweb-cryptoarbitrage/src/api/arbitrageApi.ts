import type { OrderBook, TradeResult, TradeSummary, WalletBalance, FeedStatus, CircuitBreakerState } from '../types';

const API_KEY = 'dev-key';

const headers: Record<string, string> = {
  'Content-Type': 'application/json',
  'X-API-Key': API_KEY,
};

async function get<T>(url: string): Promise<T> {
  const res = await fetch(url, { headers });
  if (!res.ok) throw new Error(`${res.status}: ${res.statusText}`);
  return res.json();
}

export const arbitrageApi = {
  getOrderBooks: (): Promise<OrderBook[]> =>
    get<OrderBook[]>('/api/orderbooks'),

  getTrades: (limit = 50): Promise<TradeResult[]> =>
    get<TradeResult[]>(`/api/trades?limit=${limit}`),

  getTradeSummary: (): Promise<TradeSummary> =>
    get<TradeSummary>('/api/trades/summary'),

  getWallets: (): Promise<WalletBalance[]> =>
    get<WalletBalance[]>('/api/status/wallets'),

  getConnections: (): Promise<FeedStatus[]> =>
    get<FeedStatus[]>('/api/status/connections'),

  getCircuitBreaker: (): Promise<CircuitBreakerState> =>
    get<CircuitBreakerState>('/api/status/circuit-breaker'),
};
