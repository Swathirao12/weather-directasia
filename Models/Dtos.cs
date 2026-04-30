namespace WeatherService.Models;

public record SubscribeAlertRequest(
    string Email,
    string Location,
    decimal? MinTemperatureC,
    decimal? MaxTemperatureC,
    int? MaxAirQualityIndex
);

public record WeatherPoint(
    DateTime ObservedAtUtc,
    decimal TemperatureC,
    decimal HumidityPercent,
    int? AirQualityIndex
);
