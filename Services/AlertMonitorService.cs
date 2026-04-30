using Microsoft.EntityFrameworkCore;
using WeatherService.Data;

namespace WeatherService.Services;

public class AlertMonitorService(
    IServiceScopeFactory scopeFactory,
    IWeatherProvider weatherProvider,
    ILogger<AlertMonitorService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            await EvaluateSubscriptionsAsync(stoppingToken);
        }
    }

    private async Task EvaluateSubscriptionsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
        var subscriptions = await db.AlertSubscriptions.AsNoTracking().ToListAsync(cancellationToken);

        foreach (var subscription in subscriptions)
        {
            var current = await weatherProvider.GetCurrentAsync(subscription.Location, cancellationToken);
            if (current is null)
            {
                continue;
            }

            var temperatureTooLow = subscription.MinTemperatureC.HasValue &&
                                    current.TemperatureC < subscription.MinTemperatureC.Value;
            var temperatureTooHigh = subscription.MaxTemperatureC.HasValue &&
                                     current.TemperatureC > subscription.MaxTemperatureC.Value;
            var aqiTooHigh = subscription.MaxAirQualityIndex.HasValue &&
                             current.AirQualityIndex.HasValue &&
                             current.AirQualityIndex.Value > subscription.MaxAirQualityIndex.Value;

            if (temperatureTooLow || temperatureTooHigh || aqiTooHigh)
            {
                logger.LogWarning(
                    "Weather alert triggered for {Email} at {Location}. Temp={TempC}C, AQI={AQI}",
                    subscription.Email,
                    subscription.Location,
                    current.TemperatureC,
                    current.AirQualityIndex
                );
            }
        }
    }
}
