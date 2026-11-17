using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WebApp1.Controllers;
using WebApp1.Data;

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

var mvcBuilder = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

builder.Services.AddMemoryCache();

builder.Services.AddDbContext<DBContext>(options =>
    options.UseNpgsql(BuildPostgresConnectionString(builder.Configuration)));

builder.Services
    .AddHttpClient("openweathermap", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WeatherSeeker/2.0");
    })
    // Redirects are refused so a compromised upstream cannot bounce us to
    // an internal address.
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // Production runs behind TLS (Cloudflare or a reverse proxy) so the
        // __Host- prefix and a Secure cookie are enforced. Local HTTP debugging
        // would silently drop such a cookie, so development relaxes them.
        var useSecureCookie = !builder.Environment.IsDevelopment();

        options.Cookie.Name = useSecureCookie
            ? "__Host-WeatherSeeker.Auth"
            : "WeatherSeeker.Dev.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = useSecureCookie
            ? CookieSecurePolicy.Always
            : CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.IsEssential = true;
        options.LoginPath = "/Home/Login";
        options.AccessDeniedPath = "/Home/Login";
        // Named so the redirect reads ?returnUrl=... instead of an anonymous ?=,
        // which was a route-value bug. Honoured only when local (see
        // LoginController), so it cannot become an open redirect.
        options.ReturnUrlParameter = "returnUrl";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);

        // Deletion and sign-out must take effect on the next request, not when the
        // cookie happens to expire. The row is the source of truth: if it is gone,
        // or its session version moved on, the cookie is no longer valid.
        options.Events.OnValidatePrincipal = async context =>
        {
            var dbContext = context.HttpContext.RequestServices.GetRequiredService<DBContext>();
            var id = AccountSession.GetAccountId(context.Principal!);

            var account = id is null
                ? null
                : await dbContext.Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == id.Value);

            if (account is null || !AccountSession.MatchesSessionVersion(context.Principal!, account))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// There are no roles: every account is an ordinary account that can read the
// forecast and delete itself. Nothing here grants privileged access.
builder.Services.AddAuthorization();

// The antiforgery cookie is a credential too, so it should carry Secure
// wherever the request is already HTTPS. Always is deliberately not used:
// the antiforgery system throws if it must issue a Secure cookie on a request
// it does not see as HTTPS, and behind a TLS-terminating proxy that is only
// detected when TRUSTED_PROXIES is configured. A missing flag is a small
// weakness; a 500 on every form is an outage.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Credential stuffing protection: a small fixed window per client address on
// the login endpoint only, so ordinary browsing is untouched.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    // Sign-up writes a row, so it is capped harder than a login attempt.
    options.AddPolicy("signup", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            }));

    // Every cache miss on the weather proxy spends the shared OpenWeatherMap key
    // quota, so one account must not be able to drain it for everyone. Partitioned
    // per account rather than per address so a shared NAT cannot starve a user.
    options.AddPolicy("weather", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 30,
                TokensPerPeriod = 30,
                ReplenishmentPeriod = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;

    // Trust only the addresses the operator declares. The previous build cleared
    // KnownProxies entirely, which let any caller forge X-Forwarded-For.
    options.KnownProxies.Clear();
    options.KnownNetworks.Clear();

    foreach (var proxy in ReadTrustedProxies(builder.Configuration))
    {
        if (IPAddress.TryParse(proxy, out var address))
        {
            options.KnownProxies.Add(address);
        }
        else
        {
            options.KnownNetworks.Add(Microsoft.AspNetCore.HttpOverrides.IPNetwork.Parse(proxy));
        }
    }
});

var app = builder.Build();

await ApplyMigrationsAsync(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;

    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Cross-Origin-Opener-Policy"] = "same-origin";
    headers["Cross-Origin-Resource-Policy"] = "same-origin";
    headers["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=(), payment=(), usb=()";
    headers["X-Permitted-Cross-Domain-Policies"] = "none";
    headers["Content-Security-Policy"] = ContentSecurityPolicy;

    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = staticFile =>
    {
        // Static assets are fingerprint-versioned by asp-append-version, so a
        // long cache lifetime is safe for everything except HTML.
        staticFile.Context.Response.Headers.CacheControl =
            app.Environment.IsDevelopment()
                ? "no-cache"
                : "public,max-age=604800";
    }
});

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static async Task ApplyMigrationsAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<DBContext>();

    try
    {
        await dbContext.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        // A database blip must not take the whole site down at boot; the error
        // is logged and the request pipeline reports the failure per request.
        logger.LogError(ex, "Database migration failed at startup.");
    }
}

static IEnumerable<string> ReadTrustedProxies(IConfiguration configuration)
{
    var raw = configuration["TRUSTED_PROXIES"];

    if (string.IsNullOrWhiteSpace(raw))
    {
        return Array.Empty<string>();
    }

    return raw
        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(entry => !string.IsNullOrWhiteSpace(entry));
}

static string BuildPostgresConnectionString(IConfiguration configuration)
{
    var host = configuration["POSTGRES_HOST"];
    var database = configuration["POSTGRES_DB"];
    var username = configuration["POSTGRES_USER"];
    var password = configuration["POSTGRES_PASSWORD"];
    var sslModeValue = configuration["POSTGRES_SSL_MODE"];

    if (string.IsNullOrWhiteSpace(host) ||
        string.IsNullOrWhiteSpace(database) ||
        string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            "Database configuration is incomplete. Set POSTGRES_HOST, POSTGRES_DB, POSTGRES_USER, and POSTGRES_PASSWORD in .env or environment variables.");
    }

    if (!int.TryParse(configuration["POSTGRES_PORT"], out var port))
    {
        port = 5432;
    }

    if (!Enum.TryParse<SslMode>(sslModeValue, true, out var sslMode))
    {
        sslMode = SslMode.Disable;
    }

    var connectionStringBuilder = new NpgsqlConnectionStringBuilder
    {
        Host = host,
        Port = port,
        Database = database,
        Username = username,
        Password = password,
        SslMode = sslMode
    };

    return connectionStringBuilder.ConnectionString;
}

static void LoadDotEnv()
{
    var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

    while (currentDirectory is not null)
    {
        var envFilePath = Path.Combine(currentDirectory.FullName, ".env");
        if (File.Exists(envFilePath))
        {
            foreach (var line in File.ReadAllLines(envFilePath))
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = trimmedLine.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = trimmedLine[..separatorIndex].Trim();
                var value = trimmedLine[(separatorIndex + 1)..].Trim().Trim('"');

                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }

            return;
        }

        currentDirectory = currentDirectory.Parent;
    }
}

partial class Program
{
    // Everything the pages actually load: same-origin assets, the inline weather
    // icon sprite, and the SVG `use` references. No remote scripts or fonts.
    private const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "object-src 'none'";
}
