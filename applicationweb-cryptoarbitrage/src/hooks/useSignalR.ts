import { useEffect, useRef, useState, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import type {
  OrderBook,
  ArbitrageOpportunity,
  TradeResult,
} from '../types';

const API_KEY = import.meta.env.VITE_API_KEY || 'dev-key';

export function useSignalR() {
  const [connected, setConnected] = useState(false);
  const [orderBooks, setOrderBooks] = useState<Record<string, OrderBook>>({});
  const [opportunities, setOpportunities] = useState<ArbitrageOpportunity[]>([]);
  const [trades, setTrades] = useState<TradeResult[]>([]);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const connect = useCallback(async () => {
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/arbitrage`, {
        accessTokenFactory: () => API_KEY,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    conn.on('OrderBookUpdated', (data: OrderBook) => {
      setOrderBooks(prev => ({ ...prev, [data.exchangeId]: data }));
    });

    conn.on('OpportunityFound', (data: ArbitrageOpportunity) => {
      setOpportunities(prev => {
        const updated = [data, ...prev];
        return updated.slice(0, 50); // Mantener últimas 50
      });
    });

    conn.on('TradeExecuted', (data: TradeResult) => {
      setTrades(prev => [data, ...prev]);
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
    connect();
    return () => {
      connectionRef.current?.stop();
    };
  }, [connect]);

  const clearOpportunities = useCallback(
    () => setOpportunities([]),
    []
  );

  return {
    connected,
    orderBooks,
    opportunities,
    trades,
    clearOpportunities,
  };
}
