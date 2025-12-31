namespace ObsLiveNowIndicator.Models;

public enum AppearanceType
{
    Star,
    Circle,
    Square
}

public enum IconPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    TopCenter,
    BottomCenter
}

public class AppSettings
{
    public string ObsUrl { get; set; } = "ws://localhost";
    public int ObsPort { get; set; } = 4455;
    public string ObsPassword { get; set; } = "";
    public AppearanceType AppearanceType { get; set; } = AppearanceType.Star;
    public bool RunOnStartup { get; set; } = true;
    public int IconSize { get; set; } = 80;
    public double IconOpacity { get; set; } = 0.9;
    public IconPosition IconPosition { get; set; } = IconPosition.TopRight;
    public bool EnablePulse { get; set; } = true;
}
