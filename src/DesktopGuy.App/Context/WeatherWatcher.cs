using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopGuy.App.Context;

/// <summary>
/// Notices what the weather is like outside, so the character can dress
/// for it. Uses two free, no-API-key services:
///   - ipapi.co to guess a city-level location from your IP address (looked
///     up once and cached for the life of the app - not polled repeatedly)
///   - Open-Meteo for the actual forecast, polled periodically
///
/// Both are plain HTTPS calls with no signup required. If either is
/// unreachable (no internet, a firewall, the service being down), this
/// quietly reports <see cref="WeatherCondition.None"/> instead of crashing
/// or blocking the rest of the character.
/// </summary>
public sealed class WeatherWatcher
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(20);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    private readonly double _coldThresholdCelsius;
    private readonly double _hotThresholdCelsius;

    private double? _latitude;
    private double? _longitude;
    private volatile WeatherCondition _current = WeatherCondition.None;

    public WeatherCondition Current => _current;

    public WeatherWatcher(double coldThresholdCelsius, double hotThresholdCelsius)
    {
        _coldThresholdCelsius = coldThresholdCelsius;
        _hotThresholdCelsius = hotThresholdCelsius;
    }

    public void Start(CancellationToken cancellationToken)
    {
        _ = PollLoopAsync(cancellationToken);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshOnceAsync(cancellationToken);
            }
            catch
            {
                _current = WeatherCondition.None;
            }

            try
            {
                await Task.Delay(PollInterval, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        if (_latitude is null || _longitude is null)
        {
            var located = await TryLocateAsync(cancellationToken);
            if (located is null)
            {
                _current = WeatherCondition.None;
                return;
            }

            (_latitude, _longitude) = located.Value;
        }

        string url =
            $"https://api.open-meteo.com/v1/forecast?latitude={_latitude:F4}&longitude={_longitude:F4}" +
            "&current_weather=true&temperature_unit=celsius";

        using var response = await Http.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var current = document.RootElement.GetProperty("current_weather");
        double temperatureCelsius = current.GetProperty("temperature").GetDouble();
        int weatherCode = current.GetProperty("weathercode").GetInt32();
        bool isDay = current.GetProperty("is_day").GetInt32() == 1;

        _current = Classify(temperatureCelsius, weatherCode, isDay);
    }

    private WeatherCondition Classify(double temperatureCelsius, int weatherCode, bool isDay)
    {
        // Priority: rain is the most disruptive/visible, then temperature
        // extremes, then "it's just nice out" as the default clear-sky case.
        if (IsRainy(weatherCode))
        {
            return WeatherCondition.Rainy;
        }

        if (temperatureCelsius <= _coldThresholdCelsius)
        {
            return WeatherCondition.Cold;
        }

        if (temperatureCelsius >= _hotThresholdCelsius)
        {
            return WeatherCondition.Hot;
        }

        if (isDay && weatherCode is 0 or 1)
        {
            return WeatherCondition.Sunny;
        }

        return WeatherCondition.None;
    }

    /// <summary>WMO weather codes (as used by Open-Meteo) that mean some kind of rain.</summary>
    private static bool IsRainy(int weatherCode) => weatherCode is
        51 or 53 or 55 or 56 or 57 or   // drizzle
        61 or 63 or 65 or 66 or 67 or   // rain
        80 or 81 or 82 or               // rain showers
        95 or 96 or 99;                 // thunderstorms

    private static async Task<(double Latitude, double Longitude)?> TryLocateAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await Http.GetAsync("https://ipapi.co/json/", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (!root.TryGetProperty("latitude", out var latitudeElement) ||
                !root.TryGetProperty("longitude", out var longitudeElement))
            {
                return null;
            }

            return (latitudeElement.GetDouble(), longitudeElement.GetDouble());
        }
        catch
        {
            return null;
        }
    }
}
