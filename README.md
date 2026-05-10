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
$env:ConnectionStrings__DefaultConnection="Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<db>;User ID=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
dotnet run --project .\WebApp1\WebApp1.csproj
```

### 2) Cloudflare deployment checklist

- DNS: point your domain record to your origin.
- SSL/TLS mode: set to `Full (strict)`.
- Edge certificates: keep HTTPS enabled and automatic HTTPS rewrites on.
- Origin: install a valid certificate (Cloudflare Origin Cert or CA-signed cert).
- App config: forwarded headers are enabled in startup so original client IP/protocol are respected.
- Cookies: auth cookie is set `Secure`, `HttpOnly`, and `SameSite=Lax`.

### 3) Authentication security

- Passwords are stored as one-way hashes using ASP.NET Core `PasswordHasher`.
- Login verifies hashed passwords and creates a secure cookie-authenticated session.
- Logout endpoint is CSRF-protected.

## Notes

- If you already have plaintext passwords in the database from older builds, those accounts need a password reset or migration to hashed values before they can log in.
