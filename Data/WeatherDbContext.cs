using Microsoft.EntityFrameworkCore;
using WeatherService.Models;

namespace WeatherService.Data;

public class WeatherDbContext(DbContextOptions<WeatherDbContext> options) : DbContext(options)
{
    public DbSet<WeatherRecord> WeatherRecords => Set<WeatherRecord>();
    public DbSet<WeatherAlertSubscription> AlertSubscriptions => Set<WeatherAlertSubscription>();
}
