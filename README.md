# WeatherSeeker2
ASP.NET MVC weather search application with cookie-based authentication.

## Features

- **Weather page** — city search and 5-day forecast (requires login).
- **Temperature converter** — Celsius to Fahrenheit, available on the weather page.
- **Client & Admin registration** — IDs are assigned automatically by the database; users only supply a username and password.
- **Login / Logout** — cookie-authenticated sessions (8-hour sliding expiry).
- **Route protection** — unauthenticated users are redirected to the login page.

## Docker Quick Start

1. Copy `.env.example` to `.env` and set secure values.
2. Build and run:

```powershell
docker compose up -d --build
```

3. Open `http://localhost:8080`.

For Cloudflare deployment steps, see `DEPLOYMENT-CLOUDFLARE.md`.

## Production Setup

### 1) Configure connection string by environment

- Development uses `appsettings.Development.json`.
- Production uses `ConnectionStrings__DefaultConnection` environment variable (recommended) or `appsettings.Production.json`.
- Keep production secrets out of source control.

PowerShell example:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Production"
$env:ConnectionStrings__DefaultConnection="Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=false"
dotnet run --project .\WebApp1\WebApp1.csproj
```

### 2) Create a new deployment database

An initial migration and SQL script are included:

- Migration: `WebApp1/Migrations/20260510165804_InitialPostgresSchema.cs`
- SQL script: `WebApp1/Migrations/InitialPostgresSchema.sql`

Option A (recommended): apply EF migrations

```powershell
$env:ASPNETCORE_ENVIRONMENT="Production"
$env:ConnectionStrings__DefaultConnection="Host=<host>;Port=5432;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require;Trust Server Certificate=false"

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

- DNS: point your domain record to your origin.
- SSL/TLS mode: set to `Full (strict)`.
- Edge certificates: keep HTTPS enabled and automatic HTTPS rewrites on.
- Origin: install a valid certificate (Cloudflare Origin Cert or CA-signed cert).
- App config: forwarded headers are enabled in startup so original client IP/protocol are respected.
- Cookies: auth cookie is set `Secure`, `HttpOnly`, and `SameSite=Lax`.

### 4) Authentication & Authorization

- Passwords are stored as one-way hashes using ASP.NET Core `PasswordHasher`.
- Login verifies hashed passwords and creates a secure cookie-authenticated session.
- Logout endpoint is CSRF-protected.
- The Weather (Index) page requires an authenticated session; unauthenticated requests redirect to Login.
- Client and Admin registration automatically creates a corresponding `Users` record; no manual ID entry is required.

## Notes

- If you already have plaintext passwords in the database from older builds, those accounts need a password reset or migration to hashed values before they can log in.
- The app runs `Database.Migrate()` at startup, so it can initialize/upgrade schema automatically when the configured account has DDL permissions.
- The default database name in `.env.example` and the Docker Compose connection string is `weatherseeker`; keep `POSTGRES_DB` and the `Database=` field in the connection string in sync.
