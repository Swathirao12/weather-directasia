using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Polly;
using Polly.Extensions.Http;
using WeatherService.Data;
using WeatherService.Middleware;
using WeatherService.Models;
using WeatherService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("ApiKey", new()
    {
        Name = "X-API-Key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "API key for Weather service"
    });
    options.AddSecurityRequirement(new()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "ApiKey"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<WeatherDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WeatherDb")));

builder.Services
    .AddHttpClient<IWeatherProvider, DataGovSgWeatherClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["WeatherProvider:DataGovSgBaseUrl"]!);
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retry => TimeSpan.FromMilliseconds(200 * Math.Pow(2, retry))));

builder.Services.AddScoped<ICsvExportService, CsvExportService>();
builder.Services.AddHostedService<AlertMonitorService>();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});

var app = builder.Build();

app.UseSwagger(options =>
{
    options.SerializeAsV2 = true;
});
app.UseSwaggerUI();
app.UseRateLimiter();
app.UseMiddleware<ApiKeyMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
    db.Database.EnsureCreated();
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            await Task.Delay(1000);
            var baseUrl = app.Urls.FirstOrDefault() ?? "http://localhost:5000";
            using var httpClient = new HttpClient();
            var json = await httpClient.GetStringAsync($"{baseUrl}/swagger/v1/swagger.json");

            var outputDir = Path.Combine(app.Environment.ContentRootPath, "openapi");
            Directory.CreateDirectory(outputDir);
            var outputFile = Path.Combine(outputDir, "openapi.json");
            await File.WriteAllTextAsync(outputFile, json);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Failed to export Swagger 2.0 file.");
        }
    });
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/weather/current", async (
    string location,
    IWeatherProvider provider,
    WeatherDbContext db,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(location))
    {
        return Results.BadRequest("location is required");
    }

    var point = await provider.GetCurrentAsync(location, cancellationToken);
    if (point is null)
    {
        return Results.NotFound();
    }

    var record = new WeatherRecord
    {
        Location = location,
        ObservedAtUtc = point.ObservedAtUtc,
        TemperatureC = point.TemperatureC,
        HumidityPercent = point.HumidityPercent,
        AirQualityIndex = point.AirQualityIndex,
        Source = "data.gov.sg"
    };
    db.WeatherRecords.Add(record);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Ok(record);
}).RequireRateLimiting("api");

app.MapGet("/api/weather/forecast", async (
    string location,
    int days,
    IWeatherProvider provider,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(location))
    {
        return Results.BadRequest("location is required");
    }

    var result = await provider.GetForecastAsync(location, days <= 0 ? 3 : days, cancellationToken);
    return Results.Ok(result);
}).RequireRateLimiting("api");

app.MapGet("/api/weather/historical", async (
    string location,
    DateTime fromUtc,
    DateTime toUtc,
    WeatherDbContext db,
    CancellationToken cancellationToken) =>
{
    var rows = await db.WeatherRecords.AsNoTracking()
        .Where(r => r.Location == location && r.ObservedAtUtc >= fromUtc && r.ObservedAtUtc <= toUtc)
        .OrderBy(r => r.ObservedAtUtc)
        .ToListAsync(cancellationToken);
    return Results.Ok(rows);
}).RequireRateLimiting("api");

app.MapGet("/api/weather/export/csv", async (
    string location,
    DateTime fromUtc,
    DateTime toUtc,
    WeatherDbContext db,
    ICsvExportService csvExporter,
    CancellationToken cancellationToken) =>
{
    var rows = await db.WeatherRecords.AsNoTracking()
        .Where(r => r.Location == location && r.ObservedAtUtc >= fromUtc && r.ObservedAtUtc <= toUtc)
        .OrderBy(r => r.ObservedAtUtc)
        .ToListAsync(cancellationToken);

    var bytes = csvExporter.ExportWeather(rows);
    return Results.File(bytes, "text/csv", $"weather_{location}_{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
}).RequireRateLimiting("api");

app.MapPost("/api/alerts/subscribe", async (
    SubscribeAlertRequest request,
    WeatherDbContext db,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Location))
    {
        return Results.BadRequest("email and location are required");
    }

    var subscription = new WeatherAlertSubscription
    {
        Email = request.Email,
        Location = request.Location,
        MinTemperatureC = request.MinTemperatureC,
        MaxTemperatureC = request.MaxTemperatureC,
        MaxAirQualityIndex = request.MaxAirQualityIndex
    };
    db.AlertSubscriptions.Add(subscription);
    await db.SaveChangesAsync(cancellationToken);
    return Results.Created($"/api/alerts/subscriptions/{subscription.Id}", subscription);
}).RequireRateLimiting("api");

app.MapGet("/api/alerts/subscriptions", async (WeatherDbContext db, CancellationToken cancellationToken) =>
{
    var subscriptions = await db.AlertSubscriptions.AsNoTracking().ToListAsync(cancellationToken);
    return Results.Ok(subscriptions);
}).RequireRateLimiting("api");

app.Run();

public partial class Program { }
