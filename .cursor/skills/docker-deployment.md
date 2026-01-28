---
name: docker-deployment
description: "Deploy TradingJournal using Docker and docker-compose"
auto-activates:
  - "deploy docker"
  - "docker compose"
  - "containerize"
  - "build container"
  - "production deploy"
---

# Skill: Docker Deployment

## When to Use This Skill

This skill activates when you need to:
- Build Docker images for API and Web
- Deploy using docker-compose
- Troubleshoot container issues
- Set up production environment

## Prerequisites

- Docker Desktop installed and running
- docker-compose available (`docker compose version`)
- TradingJournal solution builds successfully

## Step-by-Step Workflow

### Step 1: Build Images

```bash
# From project root
cd /Users/jaythaker/projects/TradingJournal

# Build all images
docker compose -f docker/docker-compose.yml build

# Or build individually
docker build -t trading-journal-api -f TradingJournal.Api/Dockerfile .
docker build -t trading-journal-web -f TradingJournal.Web/Dockerfile .
```

### Step 2: Start All Services

```bash
# Start in detached mode
docker compose -f docker/docker-compose.yml up -d

# Check status
docker compose -f docker/docker-compose.yml ps
```

**Services started:**
- `trading-journal-db` - PostgreSQL database (port 5432)
- `trading-journal-api` - REST API (port 3333)
- `trading-journal-web` - Web app (port 5000)

### Step 3: Verify Services

```bash
# Check logs
docker compose -f docker/docker-compose.yml logs -f

# Check individual service
docker logs trading-journal-api
docker logs trading-journal-web

# Health check
curl http://localhost:3333/api/health
curl http://localhost:5000
```

### Step 4: Access Applications

- **Web App:** http://localhost:5000
- **API Swagger:** http://localhost:3333/swagger
- **Database:** localhost:5432

### Step 5: Stop Services

```bash
# Stop all services
docker compose -f docker/docker-compose.yml down

# Stop and remove volumes (WARNING: deletes data)
docker compose -f docker/docker-compose.yml down -v
```

## docker-compose.yml Configuration

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:15-alpine
    container_name: trading-journal-db
    environment:
      POSTGRES_USER: tradingjournal
      POSTGRES_PASSWORD: tradingjournal
      POSTGRES_DB: tradingjournal
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U tradingjournal"]
      interval: 10s
      timeout: 5s
      retries: 5

  api:
    build:
      context: ..
      dockerfile: TradingJournal.Api/Dockerfile
    container_name: trading-journal-api
    environment:
      ConnectionStrings__DefaultConnection: "Host=postgres;Database=tradingjournal;Username=tradingjournal;Password=tradingjournal"
      JWT_SECRET: "your-production-secret-key-change-this"
      ASPNETCORE_URLS: "http://+:80"
    ports:
      - "3333:80"
    depends_on:
      postgres:
        condition: service_healthy

  web:
    build:
      context: ..
      dockerfile: TradingJournal.Web/Dockerfile
    container_name: trading-journal-web
    environment:
      ApiBaseUrl: "http://api/api/"
      ASPNETCORE_URLS: "http://+:80"
    ports:
      - "5000:80"
    depends_on:
      - api
```

## Common Issues and Solutions

**Issue:** Container exits immediately
**Solution:** Check logs for errors:
```bash
docker logs trading-journal-api
# Look for connection string or build errors
```

**Issue:** Database connection refused
**Solution:** Wait for postgres health check or check network:
```bash
# Restart with fresh network
docker compose -f docker/docker-compose.yml down
docker compose -f docker/docker-compose.yml up -d
```

**Issue:** Port already in use
**Solution:** Change ports in docker-compose.yml:
```yaml
ports:
  - "3334:80"  # Different host port
```

**Issue:** Old image cached
**Solution:** Rebuild without cache:
```bash
docker compose -f docker/docker-compose.yml build --no-cache
```

**Issue:** Web can't reach API
**Solution:** In Docker network, use service name:
```yaml
environment:
  ApiBaseUrl: "http://api/api/"  # 'api' is service name
```

## Production Checklist

- [ ] Change JWT_SECRET to strong random value
- [ ] Use volume for postgres_data persistence
- [ ] Set up HTTPS/TLS termination (nginx, traefik)
- [ ] Configure proper CORS origins
- [ ] Set up logging and monitoring
- [ ] Configure backup for database volume

## Success Criteria

- ✅ All three containers running (`docker ps`)
- ✅ Web app accessible at http://localhost:5000
- ✅ API responds at http://localhost:3333/swagger
- ✅ Database persists data across restarts
- ✅ Containers restart on failure
