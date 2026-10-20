public sealed class AppSettings
{
    public string ApiBaseUrl { get; init; }
    public StatusSource Source { get; init; } = StatusSource.TeamsLog;
    public string? TeamsLogDirectory { get; init; }
    public CaptureSettings Capture { get; init; } = new();
}