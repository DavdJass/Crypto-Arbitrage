import { useState, useEffect, useRef } from 'react';
import type { OrderBook } from '../types';

const exchangeMeta: Record<string, { color: string; cls: string }> = {
  Binance:  { color: '#f0b90b', cls: 'binance' },
  Kraken:   { color: '#5c6bc0', cls: 'kraken' },
  Bybit:    { color: '#ff8f00', cls: 'bybit' },
  Coinbase: { color: '#0052ff', cls: 'coinbase' },
  OKX:      { color: '#c8c8c8', cls: 'okx' },
  Bitfinex: { color: '#35baf2', cls: 'bitfinex' },
  KuCoin:   { color: '#0093e9', cls: 'kucoin' },
  'Gate.io':{ color: '#e65c00', cls: 'gateio' },
  Bitstamp: { color: '#50a3d9', cls: 'bitstamp' },
  Gemini:   { color: '#00d4aa', cls: 'gemini' },
};

type FlashDir = 'up' | 'down' | 'none';

function useFlash(value: number): { flash: FlashDir; ref: React.RefObject<number | null> } {
  const ref = useRef<number | null>(null);
  const [flash, setFlash] = useState<FlashDir>('none');

  useEffect(() => {
    if (ref.current === null) { ref.current = value; return; }
    if (value === ref.current) return;

    const dir: FlashDir = value > ref.current ? 'up' : 'down';
    ref.current = value;
    setFlash(dir);
    const t = setTimeout(() => setFlash('none'), 600);
    return () => clearTimeout(t);
  }, [value]);

  return { flash, ref };
}

function FlashPrice({ value, cls }: { value: number; cls: string }) {
  const { flash } = useFlash(value);
  const color = flash === 'up' ? 'var(--green)' : flash === 'down' ? 'var(--red)' : undefined;
  return (
    <span
      className={`price-value ${cls}`}
      style={{
        transition: 'color 0.2s ease',
        ...(color ? { color, fontWeight: 700 } : {}),
      }}
    >
      ${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
    </span>
  );
}

export function OrderBookPanel({ books }: { books: Record<string, OrderBook> }) {
  const entries = Object.entries(books);

  return (
    <div className="orderbook-grid">
      {entries.map(([id, book]) => {
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
              <FlashPrice value={book.bestBid} cls="green" />
            </div>

            <div className="price-row">
              <span className="price-label">Ask</span>
              <FlashPrice value={book.bestAsk} cls="red" />
            </div>

            <div className="price-row">
              <span className="price-label">Spread</span>
              <span className="price-value">
                ${spread.toFixed(2)} <span className="dim">({spreadPct.toFixed(3)}%)</span>
              </span>
            </div>

            <div className="volume-mini">
              <span>Vol Bid: {book.bidVolume.toFixed(4)}</span>
              <span>Vol Ask: {book.askVolume.toFixed(4)}</span>
            </div>
          </div>
        );
      })}

      {entries.length === 0 && (
        <div className="orderbook-card">
          <div className="price-row">
            <span className="dim">Conectando feeds en vivo…</span>
          </div>
        </div>
      )}
    </div>
  );
}
