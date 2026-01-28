---
name: local-dev-setup
description: "Set up TradingJournal local development environment for this project"
auto-activates:
  - "set up development"
  - "run locally"
  - "start project"
  - "how to run"
  - "local setup"
---

# Skill: Local Development Setup

## When to Use This Skill

This skill activates when you need to:
- Set up the TradingJournal project for local development
- Start the API and Web applications
- Troubleshoot startup issues

## Prerequisites

Check if development environment is ready:
- .NET 10.0 SDK installed (`dotnet --version`)
- Docker Desktop running (`docker ps`)
- PostgreSQL container exists (`docker ps | grep trading-journal-db`)

## Step-by-Step Workflow

### Step 1: Verify .NET SDK

```bash
dotnet --version
# Should show 10.x.x
```

**If not installed:**
```bash
# macOS with Homebrew
brew install --cask dotnet-sdk

# Or use dotnet-install script
curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
export PATH="$HOME/.dotnet:$PATH"
```

### Step 2: Start Database

```bash
cd docker
docker compose up -d postgres

# Verify database is running
docker ps | grep trading-journal-db

# Check logs if issues
docker logs trading-journal-db
```

**Connection details:**
- Host: localhost
- Port: 5432
- Database: tradingjournal
- Username: tradingjournal
- Password: tradingjournal

### Step 3: Start API

```bash
cd TradingJournal.Api
dotnet run
```

**Expected output:**
```
Now listening on: http://localhost:5122
Application started. Press Ctrl+C to shut down.
```

**Verify API is running:**
- Swagger UI: http://localhost:5122/swagger

### Step 4: Start Web Application

**In a new terminal:**
```bash
cd TradingJournal.Web
dotnet run
```

**Expected output:**
```
Now listening on: http://localhost:5026
Application started. Press Ctrl+C to shut down.
```

**Access web app:** http://localhost:5026

### Step 5: Create Test Account

1. Open http://localhost:5026
2. Click "Register"
3. Enter email and password
4. Create an account (e.g., "Fidelity IRA")
5. Start importing trades

## Common Issues and Solutions

**Issue:** Database connection refused
**Solution:**
```bash
# Check if Docker is running
docker ps

# Restart database container
docker compose -f docker/docker-compose.yml restart postgres

# Verify connection
docker exec trading-journal-db psql -U tradingjournal -d tradingjournal -c "SELECT 1;"
```

**Issue:** Port already in use
**Solution:**
```bash
# Find and kill process on port 5122 (API)
lsof -ti:5122 | xargs kill -9

# Find and kill process on port 5026 (Web)
lsof -ti:5026 | xargs kill -9
```

**Issue:** EF Core migration errors
**Solution:**
```bash
# Apply migrations directly to database
docker exec trading-journal-db psql -U tradingjournal -d tradingjournal -c "
-- Add any missing columns manually
ALTER TABLE trades ADD COLUMN IF NOT EXISTS \"instrumentType\" integer DEFAULT 0;
"
```

**Issue:** API not found from Web app
**Solution:** Verify `appsettings.json` in TradingJournal.Web has correct API URL:
```json
{
  "ApiBaseUrl": "http://localhost:5122/api/"
}
```

## Success Criteria

- ✅ PostgreSQL container running (`docker ps` shows trading-journal-db)
- ✅ API responds at http://localhost:5122/swagger
- ✅ Web app loads at http://localhost:5026
- ✅ Can register and login
- ✅ Can create accounts and import trades
