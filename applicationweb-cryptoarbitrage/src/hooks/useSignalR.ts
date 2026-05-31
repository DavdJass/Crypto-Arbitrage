import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import { arbitrageApi } from '../api/arbitrageApi';
import type {
  OrderBook,
  StoredOpportunity,
  TradeResult,
  TriangularOpportunity,
} from '../types';

const API_KEY = import.meta.env.VITE_API_KEY || 'dev-key';
const HUB_BASE = import.meta.env.VITE_BACKEND_URL || '';
const MAX_NON_OBSERVED = 15;
const MAX_OBSERVED = 15;

function pairKey(o: StoredOpportunity) {
  return `${o.buyExchange}→${o.sellExchange}`;
}

/** Dedup observadas por par (mejor spread) y limita colas. */
export function normalizeOpportunities(ops: StoredOpportunity[]): StoredOpportunity[] {
  const nonObserved = ops.filter(o => o.status !== 'observed').slice(0, MAX_NON_OBSERVED);
  const byPair = new Map<string, StoredOpportunity>();
  for (const o of ops.filter(o => o.status === 'observed')) {
    const key = pairKey(o);
    const existing = byPair.get(key);
    const spread = o.bidPrice - o.askPrice;
    const existingSpread = existing ? existing.bidPrice - existing.askPrice : -Infinity;
    if (!existing || spread > existingSpread) byPair.set(key, o);
  }
  const observed = [...byPair.values()]
    .sort((a, b) => (b.bidPrice - b.askPrice) - (a.bidPrice - a.askPrice))
    .slice(0, MAX_OBSERVED);
  return [...nonObserved, ...observed];
}

export function useSignalR() {
  const [connected, setConnected] = useState(false);
  const [orderBooks, setOrderBooks] = useState<Record<string, OrderBook>>({});
  const [opportunities, setOpportunities] = useState<StoredOpportunity[]>([]);
  const [trades, setTrades] = useState<TradeResult[]>([]);
  const [triangularOpps, setTriangularOpps] = useState<TriangularOpportunity[]>([]);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const loadHistory = useCallback(async () => {
    try {
      const [storedOps, recentTrades, books] = await Promise.all([
        arbitrageApi.getOpportunities(100),
        arbitrageApi.getTrades(50),
        arbitrageApi.getOrderBooks(),
      ]);
      setOpportunities(normalizeOpportunities(storedOps));
      setTrades(recentTrades);
      const bookMap: Record<string, OrderBook> = {};
      for (const book of books) {
        bookMap[book.exchangeId] = book;
      }
      setOrderBooks(bookMap);
    } catch (err) {
      console.warn('No se pudo cargar historial REST:', err);
    }
  }, []);

  const connect = useCallback(async () => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`${HUB_BASE}/hubs/arbitrage`, {
        accessTokenFactory: () => API_KEY,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    conn.on('OrderBookUpdated', (data: OrderBook) => {
      setOrderBooks(prev => ({ ...prev, [data.exchangeId]: data }));
    });

    conn.on('OpportunityFound', (data: StoredOpportunity) => {
      setOpportunities(prev => {
        if (data.status === 'observed') {
          const withoutPair = prev.filter(
            o => !(o.status === 'observed' && pairKey(o) === pairKey(data))
          );
          return normalizeOpportunities([...withoutPair, data]);
        }
        return normalizeOpportunities([data, ...prev.filter(o => o.status !== 'observed')]);
      });
    });

    conn.on('TradeExecuted', (data: TradeResult) => {
      setTrades(prev => [data, ...prev].slice(0, 100));
      setOpportunities(prev =>
        prev.map(op =>
          op.buyExchange === data.buyExchange &&
          op.sellExchange === data.sellExchange &&
          op.status === 'detected'
            ? { ...op, status: 'executed' }
            : op
        )
      );
    });

    conn.on('TriangularOpportunity', (data: TriangularOpportunity) => {
      setTriangularOpps(prev => [data, ...prev].slice(0, 25));
    });

    conn.onreconnecting(() => setConnected(false));
    conn.onreconnected(async () => {
      setConnected(true);
      await loadHistory();
    });

    try {
      await conn.start();
      setConnected(true);
      connectionRef.current = conn;
    } catch (err) {
      console.error('SignalR connection failed:', err);
      try {
        await conn.stop();
      } catch {
        /* ignore */
      }
      setTimeout(() => connect(), 3000);
    }
  }, [loadHistory]);

  useEffect(() => {
    let cancelled = false;

    (async () => {
      await loadHistory();
      if (!cancelled) await connect();
    })();

    return () => {
      cancelled = true;
      connectionRef.current?.stop();
    };
  }, [connect, loadHistory]);

  const clearOpportunities = useCallback(() => setOpportunities([]), []);

  return {
    connected,
    orderBooks,
    opportunities,
    trades,
    triangularOpps,
    clearOpportunities,
  };
}
