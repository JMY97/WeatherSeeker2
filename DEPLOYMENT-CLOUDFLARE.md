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

### First admin account

The admin registration page requires an existing admin, so a fresh deployment
has no way in. Seed one on first start:

```text
BOOTSTRAP_ADMIN__USERNAME=<name>
BOOTSTRAP_ADMIN__PASSWORD=<12+ character passphrase>
```

The app creates the account only when no admin with that username exists, then
logs `Bootstrap admin account created`. Remove both lines from `.env` once you
have signed in; the row stays in the database.

## 3) Run application + database

```powershell
docker compose up -d --build
```

App will be available at:

- `http://localhost:8080`

The port is published on the loopback interface only. Public access is meant to
arrive through the Cloudflare Tunnel below.

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

5. Tell the app which peer may set forwarded headers. The tunnel container
   connects over the Compose bridge network, so trust that range:

```text
TRUSTED_PROXIES=172.16.0.0/12
```

Without this the app ignores `X-Forwarded-For`/`X-Forwarded-Proto`, and every
visitor looks like the tunnel's own IP (which also collapses the login rate
limiter into one shared bucket).

### Option B: Cloudflare DNS proxy to host

1. Point an `A`/`AAAA` record to your host public IP.
2. Enable proxy (orange cloud).
3. Set SSL/TLS mode to `Full (strict)`.
4. Ensure your origin serves TLS with a valid cert (or Cloudflare origin cert behind reverse proxy).
5. Publish port 8080 beyond loopback, and set `TRUSTED_PROXIES` to your reverse
   proxy's address.

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
