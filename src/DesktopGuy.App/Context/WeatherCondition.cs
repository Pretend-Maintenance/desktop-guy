namespace DesktopGuy.App.Context;

/// <summary>
/// One of the four weather poses a character can strike - only ever set
/// manually via CharacterController.PreviewWeather (see the context menu's
/// "Preview Weather" submenu). There's no automatic weather detection
/// feeding this anymore; it wasn't reliably visible in practice (it only
/// showed up if the real forecast happened to match one of these AND you'd
/// been idle for a while at the same time), so it's preview-only for now.
/// </summary>
public enum WeatherCondition
{
    Cold,
    Hot,
    Sunny,
    Rainy,
}
