// ─── API Key configurable ──────────────────────────────────
const API_KEY = 'dev-key'; // Cambiar en producción

const headers = {
  'Content-Type': 'application/json',
  'X-API-Key': API_KEY,
};

async function get<T>(url: string): Promise<T> {
  const res = await fetch(url, { headers });
  if (!res.ok) throw new Error(`${res.status}: ${res.statusText}`);
  return res.json();
}

export const arbitrageApi = {
  getOrderBooks: () =>
    get<OrderBook[]>('/api/orderbooks'),

  getTrades: (limit = 50) =>
    get<TradeResult[]>(`/api/trades?limit=${limit}`),

  getTradeSummary: () =>
    get<TradeSummary>('/api/trades/summary'),

  getWallets: () =>
    get<WalletBalance[]>('/api/status/wallets'),

  getConnections: () =>
    get<FeedStatus[]>('/api/status/connections'),

  getCircuitBreaker: () =>
    get<CircuitBreakerState>('/api/status/circuit-breaker'),
};
