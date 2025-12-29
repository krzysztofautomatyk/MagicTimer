namespace MagicTimer.Settings;

public sealed class AppSettings
{
    public string? SoundFilePath { get; set; }
    public string? LastDuration { get; set; }
    public string? TimerFontFamily { get; set; }
    public bool EnableReminders { get; set; } = true;

    // Kolory UI
    public string BackgroundColor { get; set; } = "#0B0E14";
    public string TimerBackgroundColor { get; set; } = "#0F172A";
    public string TimerForegroundColor { get; set; } = "#E6EDF3";
    public string ProgressBarColor { get; set; } = "#58A6FF";
    public string StartButtonColor { get; set; } = "#2EA043";
    public string StopButtonColor { get; set; } = "#D73A49";
    public string BlinkColor { get; set; } = "#7F1D1D";
    public string ButtonBackgroundColor { get; set; } = "#30363D";
    public string TextColor { get; set; } = "#E6EDF3";
    public string InputBackgroundColor { get; set; } = "#111827";
    public string BannerBackgroundColor { get; set; } = "#7F1D1D";
}
