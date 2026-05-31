# Crypto Arbitrage — Bitcoin Arbitrage Bot

Sistema de detección y **simulación** de arbitraje **BTC/USDT** entre 10 exchanges, con WebSockets, motor .NET 8 y dashboard React en tiempo real (SignalR).

## Arquitectura

```text
Crypto-Arbitrage/
├── dotnetcore-cryptoarbitrage/   # API + bot (ASP.NET Core, SignalR, Channels)
└── applicationweb-cryptoarbitrage/  # Dashboard (React + Vite)
```

**Pipeline:** Feeds WS/REST → agregador de order books → detector N×N → ejecutor simulado → SQLite (opcional) + push SignalR → UI.

**Rentabilidad neta:**

```text
buyCost    = ask × volume × (1 + feeCompra)
sellGain   = bid × volume × (1 − feeVenta) − slippage
netProfit  = sellGain − buyCost − withdrawalFee
```

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Node.js 20+ (frontend)
- Docker (opcional, despliegue completo)

## Inicio rápido (local)

### Backend

```bash
cd dotnetcore-cryptoarbitrage/ArbitrageBot.API
dotnet run --launch-profile http
```

Swagger: http://localhost:5152/swagger

### Frontend

```bash
cd applicationweb-cryptoarbitrage
npm install
npm run dev
```

Dashboard: http://localhost:3000 (proxy a API en `:5152`)

### Todo con Docker

```bash
docker compose up -d
```

- Frontend: http://localhost:3000  
- API: http://localhost:5152/swagger  

## Variables de entorno

| Variable | Dónde | Descripción |
|----------|--------|-------------|
| `SQLITE_PATH` | Backend | Ruta DB SQLite (p. ej. `/data/arbitrage.db`). Sin esto → memoria |
| `Security__ApiKey` | Backend | API key REST (default en appsettings para dev) |
| `VITE_BACKEND_URL` | Frontend (build) | URL del backend en producción |
| `VITE_API_KEY` | Frontend (build) | Misma API key que el backend |

Ejemplo producción (Vercel + Railway):

```bash
# Railway
SQLITE_PATH=/data/arbitrage.db
Security__ApiKey=tu-clave-secreta

# Vercel
VITE_BACKEND_URL=https://tu-api.railway.app
VITE_API_KEY=tu-clave-secreta
```

## API principal

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/orderbooks` | Top of book por exchange |
| GET | `/api/opportunities?limit=100` | Historial de oportunidades |
| GET | `/api/trades` | Trades simulados |
| GET | `/api/trades/summary` | PnL, win rate |
| GET | `/api/status/wallets` | Balances simulados |
| WS | `/hubs/arbitrage` | SignalR en vivo |

Header: `X-API-Key: <tu-clave>`

## Tests

```bash
cd dotnetcore-cryptoarbitrage
dotnet test
```

## Exchanges (BTC/USDT)

Binance, Kraken, Bybit, Coinbase, OKX, Bitfinex, KuCoin (REST), Gate.io, Bitstamp, Gemini.

## Decisiones técnicas

- **Par único BTC/USDT** en todos los feeds para comparar precios comparables.
- **Volumen** limitado por liquidez del book y cap `MaxVolumeBtc`.
- **Circuit breaker** tras racha de trades negativos.
- **Arbitraje triangular** como estrategia adicional (mismo exchange).
- Persistencia con **SQLite** cuando `SQLITE_PATH` está definido (Docker/Railway).

Documentación detallada del backend: [dotnetcore-cryptoarbitrage/README.md](dotnetcore-cryptoarbitrage/README.md)
