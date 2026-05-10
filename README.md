# WeatherSeeker2
Uses Asp.net MVC to faciliate website

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

### 4) Authentication security

- Passwords are stored as one-way hashes using ASP.NET Core `PasswordHasher`.
- Login verifies hashed passwords and creates a secure cookie-authenticated session.
- Logout endpoint is CSRF-protected.

## Notes

- If you already have plaintext passwords in the database from older builds, those accounts need a password reset or migration to hashed values before they can log in.
- The app now runs `Database.Migrate()` at startup, so it can initialize/upgrade schema automatically when the configured account has DDL permissions.
