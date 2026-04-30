namespace WeatherService.Models;

public class WeatherAlertSubscription
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal? MinTemperatureC { get; set; }
    public decimal? MaxTemperatureC { get; set; }
    public int? MaxAirQualityIndex { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
