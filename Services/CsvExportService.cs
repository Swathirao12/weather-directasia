using System.Text;
using WeatherService.Models;

namespace WeatherService.Services;

public interface ICsvExportService
{
    byte[] ExportWeather(IEnumerable<WeatherRecord> rows);
}

public class CsvExportService : ICsvExportService
{
    public byte[] ExportWeather(IEnumerable<WeatherRecord> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Location,ObservedAtUtc,TemperatureC,HumidityPercent,AirQualityIndex,Source");
        foreach (var row in rows)
        {
            sb.AppendLine(
                $"{Escape(row.Location)},{row.ObservedAtUtc:O},{row.TemperatureC},{row.HumidityPercent},{row.AirQualityIndex},{Escape(row.Source)}"
            );
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
