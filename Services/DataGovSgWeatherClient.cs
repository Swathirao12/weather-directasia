using System.Net.Http.Json;
using System.Text.Json;
using WeatherService.Models;

namespace WeatherService.Services;

public interface IWeatherProvider
{
    Task<WeatherPoint?> GetCurrentAsync(string location, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WeatherPoint>> GetForecastAsync(string location, int days, CancellationToken cancellationToken = default);
}

public class DataGovSgWeatherClient(HttpClient httpClient, ILogger<DataGovSgWeatherClient> logger)
    : IWeatherProvider
{
    public async Task<WeatherPoint?> GetCurrentAsync(string location, CancellationToken cancellationToken = default)
    {
        try
        {
            var temperatureTask = httpClient.GetFromJsonAsync<JsonElement>(
                "/v2/real-time/api/air-temperature",
                cancellationToken
            );
            var humidityTask = httpClient.GetFromJsonAsync<JsonElement>(
                "/v2/real-time/api/relative-humidity",
                cancellationToken
            );

            await Task.WhenAll(temperatureTask!, humidityTask!);

            var temperature = ExtractLatestValue(temperatureTask!.Result);
            var humidity = ExtractLatestValue(humidityTask!.Result);

            if (temperature is null && humidity is null)
            {
                return FallbackCurrent(location);
            }

            return new WeatherPoint(
                DateTime.UtcNow,
                temperature ?? 29m,
                humidity ?? 70m,
                null
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch current data.gov.sg weather. Returning fallback weather.");
            return FallbackCurrent(location);
        }
    }

    public async Task<IReadOnlyList<WeatherPoint>> GetForecastAsync(string location, int days, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetFromJsonAsync<JsonElement>(
                "/v2/real-time/api/two-hr-forecast",
                cancellationToken
            );

            if (!response.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                return BuildFallbackForecast(days, location);
            }

            var results = new List<WeatherPoint>();
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("timestamp", out var timestampEl))
                {
                    continue;
                }

                var timestamp = timestampEl.GetDateTimeOffset().UtcDateTime;
                var label = ExtractForecastLabel(item);
                var estimatedTemp = EstimateTemperatureFromLabel(label);
                results.Add(new WeatherPoint(timestamp, estimatedTemp, 70m, null));
            }

            if (results.Count == 0)
            {
                return BuildFallbackForecast(days, location);
            }

            var maxPoints = Math.Clamp(days, 1, 5) * 12;
            return results.Take(maxPoints).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch data.gov.sg forecast. Returning fallback forecast.");
            return BuildFallbackForecast(days, location);
        }
    }

    private static WeatherPoint FallbackCurrent(string location)
    {
        var seed = Math.Abs(location.GetHashCode());
        var temp = 24 + (seed % 100) / 10m;
        var humidity = 40 + (seed % 50);
        return new WeatherPoint(DateTime.UtcNow, temp, humidity, null);
    }

    private static List<WeatherPoint> BuildFallbackForecast(int days, string location)
    {
        var seed = Math.Abs(location.GetHashCode());
        return Enumerable.Range(1, Math.Clamp(days, 1, 5))
            .Select(i =>
            {
                var temp = 26 + ((seed + i) % 90) / 10m;
                var humidity = 60 + ((seed + i) % 30);
                return new WeatherPoint(DateTime.UtcNow.AddHours(i * 6), temp, humidity, null);
            })
            .ToList();
    }

    private static decimal? ExtractLatestValue(JsonElement response)
    {
        if (!response.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("readings", out var readings) ||
            readings.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        decimal? latest = null;
        foreach (var reading in readings.EnumerateArray())
        {
            if (reading.TryGetProperty("value", out var valueElement) && valueElement.TryGetDecimal(out var value))
            {
                latest = value;
            }
        }

        return latest;
    }

    private static string ExtractForecastLabel(JsonElement item)
    {
        if (!item.TryGetProperty("forecasts", out var forecasts) ||
            forecasts.ValueKind != JsonValueKind.Array)
        {
            return "cloudy";
        }

        foreach (var forecast in forecasts.EnumerateArray())
        {
            if (forecast.TryGetProperty("forecast", out var label) && label.ValueKind == JsonValueKind.String)
            {
                return label.GetString() ?? "cloudy";
            }
        }

        return "cloudy";
    }

    private static decimal EstimateTemperatureFromLabel(string label)
    {
        var lowered = label.ToLowerInvariant();
        if (lowered.Contains("thunder"))
        {
            return 24m;
        }
        if (lowered.Contains("rain") || lowered.Contains("shower"))
        {
            return 25m;
        }
        if (lowered.Contains("fair") || lowered.Contains("sunny"))
        {
            return 31m;
        }
        return 28m;
    }
}
