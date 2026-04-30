# WeatherService

Simple .NET 10 Weather microservice with:
- Current and forecast weather endpoints by location.
- Historical storage in SQLite.
- CSV export of historical weather data.
- Alert subscription API and background alert checks.
- API key protection, rate limiting, and resilient external API calls.

## Quick Start

1. Install [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
2. From this folder:
   - `dotnet restore`
   - `dotnet build`
   - `dotnet run`
3. Open Swagger UI at `http://localhost:5000/swagger` (or the port shown in logs).
4. Set `X-API-Key` header to match `Security:ApiKey` in `appsettings.json`.
5. Swagger JSON is available in Swagger 2.0 format at:
   - Runtime endpoint: `/swagger/v1/swagger.json`
   - Saved file: `openapi/openapi.json`

## Configuration

- `ConnectionStrings:WeatherDb`: SQLite DB file path.
- `Security:ApiKey`: required request API key.
- `WeatherProvider:DataGovSgBaseUrl`: base URL for data.gov.sg API (default `https://api-open.data.gov.sg`).

## Endpoints

- `GET /health`
- `GET /api/weather/current?location=Singapore`
- `GET /api/weather/forecast?location=Singapore&days=3`
- `GET /api/weather/historical?location=Singapore&fromUtc=2026-04-01T00:00:00Z&toUtc=2026-04-30T23:59:59Z`
- `GET /api/weather/export/csv?location=Singapore&fromUtc=2026-04-01T00:00:00Z&toUtc=2026-04-30T23:59:59Z`
- `POST /api/alerts/subscribe`
- `GET /api/alerts/subscriptions`

## Notes

- Database is auto-created on startup (`EnsureCreated`).
- Alert monitor runs every 15 minutes and logs triggered alerts.
- On application startup, the service generates and writes a Swagger 2.0 file to `openapi/openapi.json`.

## Actions Completed

- Configured Swagger serialization to OpenAPI/Swagger 2.0 in `Program.cs`.
- Added startup export logic that writes `openapi/openapi.json` automatically.
- Added and tracked `openapi/openapi.json` in the repository for visualization tooling.
- Updated this README to reflect each change and how to consume the generated file.
- Retargeted the project from `.NET 8` to `.NET 10` in `WeatherService.csproj`.
- Updated package versions to `.NET 10` compatible versions in `WeatherService.csproj`.
- Updated Quick Start prerequisites to `.NET 10 SDK`.
- Added explicit `Microsoft.OpenApi` package reference to support OpenAPI tooling compatibility.
- Replaced writer-based export logic with startup HTTP fetch of `/swagger/v1/swagger.json` and file write to `openapi/openapi.json` for .NET 10 compatibility.
- Removed `Microsoft.AspNetCore.OpenApi` and explicit `Microsoft.OpenApi` package references to resolve version conflicts with `Swashbuckle.AspNetCore` on .NET 10.
- Switched weather provider from OpenWeatherMap to `data.gov.sg` to avoid API key signup friction.
- Updated provider configuration in `appsettings.json` from OpenWeather settings to `DataGovSgBaseUrl`.
- Updated `Program.cs` to use `DataGovSgWeatherClient` and persist source as `data.gov.sg`.
- Deleted obsolete provider file `Services/OpenWeatherClient.cs`.
- Added correctly named provider file `Services/DataGovSgWeatherClient.cs` (same implementation, clearer ownership).
