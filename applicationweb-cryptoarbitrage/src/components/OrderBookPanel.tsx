import { useState, useEffect } from 'react';
import type { OrderBook } from '../types';

const exchangeMeta: Record<string, { color: string; cls: string }> = {
  Binance: { color: '#f0b90b', cls: 'binance' },
  Kraken:  { color: '#5c6bc0', cls: 'kraken' },
  Bybit:   { color: '#ff8f00', cls: 'bybit' },
};

export function OrderBookPanel({ books }: { books: Record<string, OrderBook> }) {
  const [flash, setFlash] = useState<Record<string, 'up' | 'down' | null>>({});
  const prevPrices = useState<Record<string, number>>({})[0];

  useEffect(() => {
    Object.entries(books).forEach(([id, book]) => {
      const prev = prevPrices[id];
      if (prev && prev !== book.bestBid) {
        const dir = book.bestBid > prev ? 'up' : 'down';
        setFlash(f => ({ ...f, [id]: dir }));
        setTimeout(() => setFlash(f => ({ ...f, [id]: null })), 500);
      }
      prevPrices[id] = book.bestBid;
    });
  }, [books]);

  return (
    <div className="orderbook-grid">
      {Object.entries(books).map(([id, book]) => {
        const meta = exchangeMeta[id] || { color: '#666', cls: '' };
        const spread = book.bestAsk - book.bestBid;
        const spreadPct = book.bestBid ? (spread / book.bestBid) * 100 : 0;
        const isFlash = flash[id];

        return (
          <div key={id} className={`orderbook-card ${meta.cls}`}>
            <div className="exchange-header">
              <span className="exchange-dot" style={{ background: meta.color }} />
              <span className="exchange-name" style={{ color: meta.color }}>{id}</span>
            </div>
            <div className="price-row">
              <span className="price-label">Bid</span>
              <span className={`price-value green ${isFlash === 'up' ? 'price-up' : ''}`}>
                ${book.bestBid.toLocaleString(undefined, { minimumFractionDigits: 2 })}
              </span>
            </div>
            <div className="price-row">
              <span className="price-label">Ask</span>
              <span className={`price-value red ${isFlash === 'down' ? 'price-down' : ''}`}>
                ${book.bestAsk.toLocaleString(undefined, { minimumFractionDigits: 2 })}
              </span>
            </div>
            <div className="price-row">
              <span className="price-label">Spread</span>
              <span className="price-value">${spread.toFixed(2)} <span className="dim">({spreadPct.toFixed(3)}%)</span></span>
            </div>
            <div className="volume-mini">
              <span>Vol Bid: {book.bidVolume.toFixed(4)}</span>
              <span>Vol Ask: {book.askVolume.toFixed(4)}</span>
            </div>
          </div>
        );
      })}
      {Object.keys(books).length === 0 && (
        <div className="orderbook-card">
          <div className="price-row">
            <span className="dim">Conectando feeds en vivo...</span>
          </div>
        </div>
      )}
    </div>
  );
}
