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
      setOpportunities(storedOps);
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
          // Upsert: reemplaza si ya existe ese par, si no lo agrega.
          // Después ordena por spread desc y muestra solo los mejores 15.
          const others = prev.filter(
            o => !(o.status === 'observed' &&
                   o.buyExchange === data.buyExchange &&
                   o.sellExchange === data.sellExchange)
          );
          const updatedObserved = [...others.filter(o => o.status === 'observed'), data]
            .sort((a, b) => (b.bidPrice - b.askPrice) - (a.bidPrice - a.askPrice))
            .slice(0, 15);
          return [...prev.filter(o => o.status !== 'observed'), ...updatedObserved];
        }
        // "detected" / "executed": evento nuevo al principio, máx 15
        return [data, ...prev.filter(o => o.status !== 'observed')]
          .slice(0, 15)
          .concat(prev.filter(o => o.status === 'observed'));
      });
    });

    conn.on('TradeExecuted', (data: TradeResult) => {
      setTrades(prev => [data, ...prev].slice(0, 100));
      // Mark the corresponding opportunity as executed
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
    conn.onreconnected(() => setConnected(true));

    try {
      await conn.start();
      setConnected(true);
      connectionRef.current = conn;
    } catch (err) {
      console.error('SignalR connection failed:', err);
      setTimeout(() => connect(), 3000);
    }
  }, []);

  useEffect(() => {
    void loadHistory();
    void connect();
    return () => {
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
