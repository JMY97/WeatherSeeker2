using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace WebApp1.Controllers
{
    /// <summary>
    /// Server-side proxy for OpenWeatherMap. The API key never leaves the server,
    /// callers can only pass city names / coordinates that we validate, and results
    /// are cached briefly so a single page load cannot burn through the quota.
    /// </summary>
    [Authorize]
    [Route("weather")]
    [EnableRateLimiting("weather")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class WeatherController : Controller
    {
        private const string HttpClientName = "openweathermap";
        private static readonly TimeSpan ForecastTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan GeocodeTtl = TimeSpan.FromHours(12);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<WeatherController> _logger;
        private readonly string? _apiKey;
        private readonly bool _configured;

        public WeatherController(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache,
            ILogger<WeatherController> logger,
            IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _cache = cache;
            _logger = logger;
            _apiKey = configuration["OpenWeatherMap:ApiKey"]?.Trim();
            _configured = !string.IsNullOrWhiteSpace(_apiKey);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string? city, CancellationToken cancellationToken)
        {
            if (!TryGetApiKey(out var apiKey))
            {
                return NotConfigured();
            }

            var cityName = NormalizeCity(city);
            if (cityName is null)
            {
                return BadRequest(Problem("Enter a city name of 1 to 100 characters."));
            }

            var coords = await GeocodeAsync(cityName, apiKey, cancellationToken);
            if (coords is null)
            {
                return NotFound(Problem($"No station found for “{cityName}”."));
            }

            return await BuildForecastAsync(coords.Value.Name, coords.Value.Lat, coords.Value.Lon, apiKey, cancellationToken);
        }

        [HttpGet("coords")]
        public async Task<IActionResult> Coords(
            [FromQuery] double lat,
            [FromQuery] double lon,
            CancellationToken cancellationToken)
        {
            if (!TryGetApiKey(out var apiKey))
            {
                return NotConfigured();
            }

            if (!IsValidLatitude(lat) || !IsValidLongitude(lon))
            {
                return BadRequest(Problem("Coordinates are outside the valid range."));
            }

            var name = await ReverseGeocodeAsync(lat, lon, apiKey, cancellationToken) ?? "Current location";
            return await BuildForecastAsync(name, lat, lon, apiKey, cancellationToken);
        }

        private async Task<IActionResult> BuildForecastAsync(
            string name,
            double latitude,
            double longitude,
            string apiKey,
            CancellationToken cancellationToken)
        {
            var cacheKey = string.Create(
                CultureInfo.InvariantCulture,
                $"owm:forecast:{latitude:F3}:{longitude:F3}");

            if (!_cache.TryGetValue(cacheKey, out ForecastPayload? payload) || payload is null)
            {
                var forecast = await FetchForecastAsync(latitude, longitude, apiKey, cancellationToken);
                if (forecast is null)
                {
                    return StatusCode(
                        StatusCodes.Status502BadGateway,
                        Problem("The weather service did not return usable data. Try again shortly."));
                }

                payload = forecast;
                _cache.Set(cacheKey, payload, ForecastTtl);
            }

            return Ok(new
            {
                station = name,
                latitude = Math.Round(latitude, 4),
                longitude = Math.Round(longitude, 4),
                payload.ObservedAt,
                payload.Current,
                payload.Days
            });
        }

        private async Task<ForecastPayload?> FetchForecastAsync(
            double latitude,
            double longitude,
            string apiKey,
            CancellationToken cancellationToken)
        {
            var url = string.Create(
                CultureInfo.InvariantCulture,
                $"https://api.openweathermap.org/data/2.5/forecast?lat={latitude:F4}&lon={longitude:F4}&units=metric&appid={Uri.EscapeDataString(apiKey)}");

            using var response = await SendAsync(url, cancellationToken);
            if (response is null || !response.IsSuccessStatusCode)
            {
                LogUpstreamFailure("forecast", response?.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("list", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var slots = new List<Slot>();
            foreach (var entry in list.EnumerateArray())
            {
                if (TryReadSlot(entry, out var slot))
                {
                    slots.Add(slot);
                }
            }

            if (slots.Count == 0)
            {
                return null;
            }

            // One entry per local calendar day so the strip shows five distinct days.
            // High/low are aggregated across that day's slots — a single 3-hour
            // block's temp_max is not the day's high.
            var days = slots
                .GroupBy(s => s.LocalDate)
                .Select(g => new
                {
                    Date = g.Key,
                    High = g.Max(s => s.High),
                    Low = g.Min(s => s.Low),
                    Slot = g.First()
                })
                .OrderBy(d => d.Date)
                .Take(5)
                .Select(d => new
                {
                    date = d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    label = d.Date.ToString("ddd", CultureInfo.InvariantCulture),
                    high = Math.Round(d.High),
                    low = Math.Round(d.Low),
                    condition = d.Slot.Condition,
                    icon = d.Slot.Icon
                })
                .ToArray();

            var now = slots[0];

            return new ForecastPayload(
                now.ObservedAt,
                new
                {
                    temperature = Math.Round(now.Temperature, 1),
                    feelsLike = Math.Round(now.FeelsLike, 1),
                    condition = now.Condition,
                    icon = now.Icon,
                    wind = Math.Round(now.Wind, 1),
                    gust = now.Gust is null ? (double?)null : Math.Round(now.Gust.Value, 1),
                    humidity = now.Humidity,
                    pressure = now.Pressure,
                    observedAt = now.ObservedAt
                },
                days);
        }

        private static bool TryReadSlot(JsonElement entry, out Slot slot)
        {
            slot = default;

            if (!entry.TryGetProperty("dt_txt", out var dtText) ||
                dtText.ValueKind != JsonValueKind.String ||
                !DateTime.TryParse(
                    dtText.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var observedAt))
            {
                return false;
            }

            if (!entry.TryGetProperty("main", out var main) ||
                !main.TryGetProperty("temp", out var temp) ||
                temp.ValueKind != JsonValueKind.Number)
            {
                return false;
            }

            var high = main.TryGetProperty("temp_max", out var max) && max.ValueKind == JsonValueKind.Number
                ? max.GetDouble()
                : temp.GetDouble();
            var low = main.TryGetProperty("temp_min", out var min) && min.ValueKind == JsonValueKind.Number
                ? min.GetDouble()
                : temp.GetDouble();
            var feelsLike = main.TryGetProperty("feels_like", out var feels) && feels.ValueKind == JsonValueKind.Number
                ? feels.GetDouble()
                : temp.GetDouble();
            var humidity = main.TryGetProperty("humidity", out var h) && h.ValueKind == JsonValueKind.Number
                ? h.GetInt32()
                : 0;
            var pressure = main.TryGetProperty("pressure", out var p) && p.ValueKind == JsonValueKind.Number
                ? p.GetInt32()
                : 0;

            double wind = 0;
            double? gust = null;
            if (entry.TryGetProperty("wind", out var windElement))
            {
                if (windElement.TryGetProperty("speed", out var speed) && speed.ValueKind == JsonValueKind.Number)
                {
                    wind = speed.GetDouble();
                }

                if (windElement.TryGetProperty("gust", out var gustElement) && gustElement.ValueKind == JsonValueKind.Number)
                {
                    gust = gustElement.GetDouble();
                }
            }

            var condition = string.Empty;
            var icon = string.Empty;
            if (entry.TryGetProperty("weather", out var weather) &&
                weather.ValueKind == JsonValueKind.Array &&
                weather.GetArrayLength() > 0)
            {
                var first = weather[0];
                if (first.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String)
                {
                    condition = description.GetString() ?? string.Empty;
                }

                if (first.TryGetProperty("icon", out var iconElement) && iconElement.ValueKind == JsonValueKind.String)
                {
                    icon = SanitizeIconCode(iconElement.GetString());
                }
            }

            slot = new Slot(
                observedAt,
                DateOnly.FromDateTime(observedAt),
                temp.GetDouble(),
                high,
                low,
                feelsLike,
                wind,
                gust,
                humidity,
                pressure,
                condition,
                icon);

            return true;
        }

        private async Task<(string Name, double Lat, double Lon)?> GeocodeAsync(
            string cityName,
            string apiKey,
            CancellationToken cancellationToken)
        {
            var cacheKey = "owm:geo:" + cityName.ToLowerInvariant();

            var cached = _cache.Get<GeoPoint?>(cacheKey);
            if (cached.HasValue)
            {
                return (cached.Value.Name, cached.Value.Lat, cached.Value.Lon);
            }

            var url = $"https://api.openweathermap.org/geo/1.0/direct?q={Uri.EscapeDataString(cityName)}&limit=1&appid={Uri.EscapeDataString(apiKey)}";

            using var response = await SendAsync(url, cancellationToken);
            if (response is null || !response.IsSuccessStatusCode)
            {
                LogUpstreamFailure("geocode", response?.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return null;
            }

            var first = document.RootElement[0];
            if (!first.TryGetProperty("lat", out var lat) || lat.ValueKind != JsonValueKind.Number ||
                !first.TryGetProperty("lon", out var lon) || lon.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            var name = first.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString() ?? cityName
                : cityName;

            if (first.TryGetProperty("country", out var country) &&
                country.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(country.GetString()))
            {
                name = $"{name}, {country.GetString()!.ToUpperInvariant()}";
            }

            var point = new GeoPoint(name, lat.GetDouble(), lon.GetDouble());
            _cache.Set(cacheKey, point, GeocodeTtl);

            return (point.Name, point.Lat, point.Lon);
        }

        private async Task<string?> ReverseGeocodeAsync(
            double latitude,
            double longitude,
            string apiKey,
            CancellationToken cancellationToken)
        {
            var url = string.Create(
                CultureInfo.InvariantCulture,
                $"https://api.openweathermap.org/geo/1.0/reverse?lat={latitude:F4}&lon={longitude:F4}&limit=1&appid={Uri.EscapeDataString(apiKey)}");

            using var response = await SendAsync(url, cancellationToken);
            if (response is null || !response.IsSuccessStatusCode)
            {
                LogUpstreamFailure("reverse-geocode", response?.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            {
                return null;
            }

            var first = document.RootElement[0];
            if (!first.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var name = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (first.TryGetProperty("country", out var country) &&
                country.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(country.GetString()))
            {
                return $"{name}, {country.GetString()!.ToUpperInvariant()}";
            }

            return name;
        }

        private async Task<HttpResponseMessage?> SendAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    response.Dispose();
                    _logger.LogError("OpenWeatherMap rejected the configured API key.");
                    return null;
                }

                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenWeatherMap request failed.");
                return null;
            }
        }

        private bool TryGetApiKey(out string apiKey)
        {
            apiKey = _apiKey ?? string.Empty;
            return _configured;
        }

        private IActionResult NotConfigured() =>
            StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                Problem("Weather is unavailable: no OpenWeatherMap API key is configured on the server."));

        private void LogUpstreamFailure(string operation, HttpStatusCode? status) =>
            _logger.LogWarning("OpenWeatherMap {Operation} failed with status {Status}.", operation, status);

        private static object Problem(string detail) => new { error = detail };

        private static string? NormalizeCity(string? city)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return null;
            }

            // Collapse whitespace and cap the length so the upstream URL stays bounded.
            var cleaned = string.Join(
                ' ',
                city.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            return cleaned.Length is 0 or > 100 ? null : cleaned;
        }

        private static string SanitizeIconCode(string? icon) =>
            !string.IsNullOrWhiteSpace(icon) &&
            icon.Length == 3 &&
            icon.All(c => char.IsAsciiLetterOrDigit(c))
                ? icon
                : "01d";

        private static bool IsValidLatitude(double lat) => lat is >= -90 and <= 90 && double.IsFinite(lat);

        private static bool IsValidLongitude(double lon) => lon is >= -180 and <= 180 && double.IsFinite(lon);

        private readonly record struct Slot(
            DateTime ObservedAt,
            DateOnly LocalDate,
            double Temperature,
            double High,
            double Low,
            double FeelsLike,
            double Wind,
            double? Gust,
            int Humidity,
            int Pressure,
            string Condition,
            string Icon);

        private readonly record struct GeoPoint(string Name, double Lat, double Lon);

        private sealed record ForecastPayload(DateTime ObservedAt, object Current, object Days);
    }
}
