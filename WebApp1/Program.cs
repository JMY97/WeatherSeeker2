using WebApp1.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.Cookies;
using Npgsql;

LoadDotEnv();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var mvcBuilder = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

var postgresConnectionString = BuildPostgresConnectionString(builder.Configuration);
builder.Services.AddDbContext<DBContext>(options => options.UseNpgsql(postgresConnectionString));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/Login";
        options.AccessDeniedPath = "/Home/Login";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DBContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

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
