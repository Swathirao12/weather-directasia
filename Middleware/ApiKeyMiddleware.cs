namespace WeatherService.Middleware;

public class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string HeaderName = "X-API-Key";
    private readonly string? _expectedApiKey = configuration["Security:ApiKey"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger"))
        {
            await next(context);
            return;
        }

        if (string.IsNullOrWhiteSpace(_expectedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsync("Server API key is not configured.");
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var provided) ||
            provided != _expectedApiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Missing or invalid API key.");
            return;
        }

        await next(context);
    }
}
