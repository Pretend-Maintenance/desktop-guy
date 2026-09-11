namespace DesktopGuy.App.Context;

/// <summary>The single weather "mood" that currently applies, already prioritized (rain beats temperature beats clear skies).</summary>
public enum WeatherCondition
{
    None,
    Cold,
    Hot,
    Sunny,
    Rainy,
}
