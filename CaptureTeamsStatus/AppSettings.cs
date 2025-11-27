public sealed class AppSettings
{
    public string ApiBaseUrl { get; init; }
    public CaptureSettings Capture { get; init; } = new();
}