# WeatherSeeker2

ASP.NET Core MVC weather console with cookie-based authentication. Search a city
or share your location, read the current observation, and see a five-day outlook
drawn as a temperature trace.

## Features

- **Station console** — city search, "use my location", current conditions, and a
  five-day outlook rendered as a high/low temperature trace (requires login).
- **Temperature converter** — Celsius to Fahrenheit, live on the console.
- **Client self-registration** — users pick a username and password; the account
  ID is assigned by the database.
- **Admin management** — new admin accounts can only be created by an existing
  admin, or seeded on first boot from configuration.
- **Login / Logout** — cookie-authenticated sessions (8-hour sliding expiry).
- **Dark and light themes** — follows the OS preference, with a manual toggle.
- **Route protection** — every data page redirects unauthenticated visitors.

## Architecture notes

- **The weather key stays on the server.** `WeatherController` proxies
  OpenWeatherMap and returns a trimmed JSON shape, so no API key reaches a
  browser. Results are cached in memory (10 min for forecasts, 12 h for
  geocoding).
- **Static assets are first-party.** Fonts, icons, and CSS are served from
  `wwwroot`; there is no CDN, no Google Fonts request, and no jQuery/Bootstrap.
- **Strict Content-Security-Policy.** `script-src 'self'` and `style-src 'self'`
  with no inline allowances, so views carry no inline scripts or `style=`
  attributes.

## Docker Quick Start

1. Copy `.env.example` to `.env` and set secure values.
2. Create an OpenWeatherMap account and generate an API key from
   `https://openweathermap.org/api`.
3. Set `OPENWEATHERMAP__APIKEY` in `.env` so the console can read conditions.
4. Set `BOOTSTRAP_ADMIN__USERNAME` and `BOOTSTRAP_ADMIN__PASSWORD` to create the
   first admin on startup (12+ character passphrase). Once you can sign in,
   remove those two lines.
5. Build and run:

```powershell
docker compose up -d --build
```

6. Open `http://localhost:8080`.

The app port is published on `127.0.0.1` only. Public traffic is expected to
arrive through the Cloudflare Tunnel profile — see
`DEPLOYMENT-CLOUDFLARE.md`.

## Production Setup

### 1) Configure database settings in `.env`

- Copy `.env.example` to `.env` and fill in `POSTGRES_HOST`, `POSTGRES_PORT`,
  `POSTGRES_DB`, `POSTGRES_USER`, and `POSTGRES_PASSWORD`.
- For production TLS, set `POSTGRES_SSL_MODE=Require`.
- `.env` is git-ignored. Only `.env.example` is tracked — keep it free of real
  values.

### 2) Create a new deployment database

An initial migration and SQL script are included:

- Migration: `WebApp1/Migrations/20260510165804_InitialPostgresSchema.cs`
- SQL script: `WebApp1/Migrations/InitialPostgresSchema.sql`

Option A (recommended): apply EF migrations

```powershell
$env:ASPNETCORE_ENVIRONMENT="Production"

# from repo root
& "$env:USERPROFILE\.dotnet\tools\dotnet-ef" database update --project .\WebApp1\WebApp1.csproj --startup-project .\WebApp1\WebApp1.csproj
```

Option B: run SQL directly

```powershell
# replace placeholders and run on your PostgreSQL target
$env:PGPASSWORD="<password>"
psql -h <host> -p 5432 -U <user> -d <db> -f .\WebApp1\Migrations\InitialPostgresSchema.sql
```

### 3) Cloudflare deployment checklist

- DNS: point your domain record at the tunnel.
- SSL/TLS mode: set to `Full (strict)`.
- Edge certificates: keep HTTPS enabled and automatic HTTPS rewrites on.
- Set `TRUSTED_PROXIES` to the address or CIDR range the app sees as its peer
  (for the tunnel container, the Docker bridge range). The app only honours
  `X-Forwarded-For` / `X-Forwarded-Proto` from those peers; without it, every
  visitor appears to share one IP.
- Cookies: the auth cookie uses the `__Host-` prefix with `Secure`, `HttpOnly`,
  and `SameSite=Lax` under Production.

### 4) Authentication & Authorization

- Passwords are stored as one-way hashes using ASP.NET Core `PasswordHasher`.
- Usernames must be 3–100 characters; passwords at least 12.
- Login failures return one generic message, so an unknown username and a wrong
  password are indistinguishable.
- Sign-in is rate limited to 10 attempts per 5 minutes per client address;
  client sign-up to 5 per hour.
- Logout is POST-only and CSRF-protected.
- Admin registration requires the `Admin` role.

### 5) Security headers

Set on every response:

| Header | Value |
|---|---|
| `Content-Security-Policy` | `default-src 'self'`, no inline script or style |
| `X-Content-Type-Options` | `nosniff` |
| `X-Frame-Options` / `frame-ancestors` | `DENY` / `'none'` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Cross-Origin-Opener-Policy` | `same-origin` |
| `Cross-Origin-Resource-Policy` | `same-origin` |
| `Permissions-Policy` | geolocation self-only; camera/mic/payment/usb off |
| `Strict-Transport-Security` | set by `UseHsts()` outside Development |

## Notes

- If you already have plaintext passwords in the database from older builds,
  those accounts need a password reset or migration to hashed values before they
  can log in.
- The app runs `Database.Migrate()` at startup, so it can initialize/upgrade the
  schema automatically when the configured account has DDL permissions.
- A failed migration is logged rather than crashing the process, so the site can
  still serve its error page.
- The default database name in `.env.example` is `weatherseeker`; keep
  `POSTGRES_DB` aligned with the database you actually provision.
- Weather data comes from OpenWeatherMap. Set `OPENWEATHERMAP__APIKEY` in `.env`
  for Docker and local runs.
