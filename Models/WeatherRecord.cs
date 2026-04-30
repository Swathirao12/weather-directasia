namespace WeatherService.Models;

public class WeatherRecord
{
    public int Id { get; set; }
    public string Location { get; set; } = string.Empty;
    public DateTime ObservedAtUtc { get; set; }
    public decimal TemperatureC { get; set; }
    public decimal HumidityPercent { get; set; }
    public int? AirQualityIndex { get; set; }
    public string Source { get; set; } = string.Empty;
}
