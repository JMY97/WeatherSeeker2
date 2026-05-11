# Cloudflare Deployment Guide (Docker)

This guide deploys WeatherSeeker with Docker Compose using:

- ASP.NET app container
- PostgreSQL container
- Optional Cloudflare Tunnel container for secure public access

## 1) Prerequisites

- Docker Desktop or Docker Engine + Compose plugin
- A Cloudflare-managed domain
- A Cloudflare Tunnel token (if using the tunnel option)

## 2) Configure environment

From repository root:

```powershell
Copy-Item .env.example .env
```

Edit `.env` and set at minimum:

- `POSTGRES_HOST`
- `POSTGRES_PORT`
- `POSTGRES_DB`
- `POSTGRES_USER`
- `POSTGRES_PASSWORD`
- `POSTGRES_SSL_MODE`
- `OPENWEATHERMAP__APIKEY` (from `https://openweathermap.org/api`)

For Docker Compose on the default network, keep `POSTGRES_HOST=postgres` unless you intentionally rename the database service.

## 3) Run application + database

```powershell
docker compose up -d --build
```

App will be available at:

- `http://localhost:8080`

## 4) Cloudflare public exposure

### Option A (recommended): Cloudflare Tunnel

1. In Cloudflare Zero Trust, create a tunnel and copy the token.
2. Put token into `.env`:

```text
CF_TUNNEL_TOKEN=<your-token>
```

3. Start tunnel profile:

```powershell
docker compose --profile cloudflare up -d
```

4. In Cloudflare Tunnel Public Hostname settings, route your hostname to:

- Service type: `HTTP`
- URL: `webapp:8080`

### Option B: Cloudflare DNS proxy to host

1. Point an `A`/`AAAA` record to your host public IP.
2. Enable proxy (orange cloud).
3. Set SSL/TLS mode to `Full (strict)`.
4. Ensure your origin serves TLS with a valid cert (or Cloudflare origin cert behind reverse proxy).

## 5) Migrations and schema

The app runs `Database.Migrate()` at startup, so schema is applied automatically if DB credentials have privileges.

## 6) Operations

Start:

```powershell
docker compose up -d
```

Logs:

```powershell
docker compose logs -f webapp
```

Stop:

```powershell
docker compose down
```

Stop and remove DB volume (destructive):

```powershell
docker compose down -v
```
