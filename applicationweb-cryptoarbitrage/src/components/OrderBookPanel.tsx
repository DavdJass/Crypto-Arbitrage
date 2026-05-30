import { useState, useEffect } from 'react';
import type { OrderBook } from '../types';

const exchangeMeta: Record<string, { color: string; cls: string }> = {
  Binance: { color: '#f0b90b', cls: 'binance' },
  Kraken:  { color: '#5c6bc0', cls: 'kraken' },
  Bybit:   { color: '#ff8f00', cls: 'bybit' },
  Coinbase:{ color: '#0052ff', cls: 'coinbase' },
  OKX:     { color: '#1a1a1a', cls: 'okx' },
  Bitfinex:{ color: '#35baf2', cls: 'bitfinex' },
  'KuCoin':{ color: '#0093e9', cls: 'kucoin' },
  'Gate.io':{color: '#e65c00', cls: 'gateio' },
  Bitstamp:{ color: '#50a3d9', cls: 'bitstamp' },
  Gemini:  { color: '#00d4aa', cls: 'gemini' },
};

export function OrderBookPanel({ books }: { books: Record<string, OrderBook> }) {
  const prevPrices = useState<Record<string, number>>({})[0];

  return (
    <div className="orderbook-grid">
      {Object.entries(books).map(([id, book]) => {
        const meta = exchangeMeta[id] || { color: '#666', cls: '' };
        const spread = book.bestAsk - book.bestBid;
        const spreadPct = book.bestBid ? (spread / book.bestBid) * 100 : 0;

        return (
          <div key={id} className={`orderbook-card ${meta.cls}`} style={{ borderLeft: `3px solid ${meta.color}` }}>
            <div className="exchange-header">
              <span className="exchange-dot" style={{ background: meta.color }} />
              <span className="exchange-name" style={{ color: meta.color }}>{id}</span>
            </div>
            <div className="price-row">
              <span className="price-label">Bid</span>
              <span className="price-value green">
                ${book.bestBid.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
              </span>
            </div>
            <div className="price-row">
              <span className="price-label">Ask</span>
              <span className="price-value red">
                ${book.bestAsk.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
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
          <div className="price-row"><span className="dim">Conectando feeds en vivo...</span></div>
        </div>
      )}
    </div>
  );
}
