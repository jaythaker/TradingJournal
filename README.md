# Trading Journal

An open-source trading journal application built with .NET 10.0. Track your trades, manage multiple accounts, analyze your portfolio performance, and track dividends.

## Technology Stack

### Backend (API)
- **ASP.NET Core 10.0 Web API** - RESTful API
- **Entity Framework Core 10.0** - ORM with PostgreSQL
- **JWT Authentication** - Secure API access
- **Yahoo Finance API** - Live stock quotes

### Frontend (WebApp)
- **ASP.NET Core MVC** - Web application
- **Razor Views** - Server-side rendering
- **Bootstrap 5** - UI framework
- **Chart.js** - Data visualization
- **Cookie Authentication** - Session management

## Features

- **User Authentication** - Secure JWT-based authentication
- **Multi-Account Management** - Track multiple brokerage accounts
- **Trade Tracking** - Log BUY/SELL transactions with full FIFO P&L calculation
- **Trade Import** - Import trades from Fidelity CSV exports
- **Portfolio Management** - Real-time portfolio with live stock prices
- **Dividend Tracking** - Track dividend income by symbol
- **Dashboard** - Visual charts for P&L, equity curve, and dividends
- **Summary Statistics** - Advanced metrics (Profit Factor, Win Rate, Max Drawdown, Kelly Criterion, etc.)
- **Trade Analysis** - Monthly/Weekly breakdown with win/loss tracking

## Quick Start

### Prerequisites
- .NET 10.0 SDK
- PostgreSQL 15+
- Docker (optional)

### Option 1: Using Docker

```bash
# Start PostgreSQL and both services
docker compose -f docker/docker-compose.yml up -d
```

Access at http://localhost:5000

### Option 2: Manual Setup

1. **Start PostgreSQL:**
   ```bash
   docker run -d --name trading-journal-db \
     -e POSTGRES_USER=tradingjournal \
     -e POSTGRES_PASSWORD=tradingjournal \
     -e POSTGRES_DB=tradingjournal \
     -p 5432:5432 postgres:15-alpine
   ```

2. **Run migrations:**
   ```bash
   cd TradingJournal.Api
   dotnet ef database update
   ```

3. **Start API (Terminal 1):**
   ```bash
   cd TradingJournal.Api
   dotnet run --urls "http://localhost:3333"
   ```

4. **Start WebApp (Terminal 2):**
   ```bash
   cd TradingJournal.Web
   dotnet run --urls "http://localhost:5000"
   ```

5. **Access application:**
   - Open http://localhost:5000
   - Register a new account
   - Start tracking your trades!

## Project Structure

```
TradingJournal/
├── TradingJournal.Api/           # Web API project
│   ├── Controllers/              # API endpoints
│   ├── Models/                   # Domain models
│   ├── Services/                 # Business logic
│   │   └── Import/               # CSV importers
│   └── Data/                     # DbContext
├── TradingJournal.Web/           # MVC WebApp
│   ├── Controllers/              # MVC controllers
│   ├── Views/                    # Razor views
│   ├── Models/                   # DTOs
│   └── Services/                 # API client
├── docker/                       # Docker configuration
└── TradingJournal.sln           # Solution file
```

## API Endpoints

| Endpoint | Description |
|----------|-------------|
| `POST /api/auth/register` | Register new user |
| `POST /api/auth/login` | Login and get JWT token |
| `GET /api/accounts` | List accounts |
| `GET /api/trades` | List trades |
| `POST /api/import/trades` | Import trades from CSV |
| `GET /api/portfolio` | Get portfolio positions |
| `GET /api/portfolio/with-quotes` | Portfolio with live prices |
| `GET /api/dividends` | List dividends |
| `GET /api/dashboard/metrics` | Dashboard metrics |
| `GET /api/summary` | Summary statistics |

## Development

### Build
```bash
dotnet build
```

### Database Migrations
```bash
cd TradingJournal.Api
dotnet ef migrations add MigrationName
dotnet ef database update
```

## Screenshots

The application includes:
- **Dashboard** - P&L charts, equity curve, win rate, dividends
- **Summary** - Profit factor, Kelly criterion, max drawdown, day-of-week analysis
- **Trades** - All trades, grouped by symbol, monthly/weekly analysis
- **Portfolio** - Live positions with real-time quotes
- **Dividends** - Dividend tracking and analysis
- **Import** - CSV import for Fidelity exports

## License

MIT License
