using Serilog;
using System.Drawing;

public sealed class ScreenStatusSource : ITeamsStatusSource
{
    private readonly ScreenCaptureService _captureService;
    private readonly StatusDetector _detector = new();

    public ScreenStatusSource(CaptureSettings capture)
    {
        _captureService = new ScreenCaptureService(
            new Rectangle(capture.X, capture.Y, capture.Width, capture.Height),
            new Rectangle(capture.CircleOffsetX, capture.CircleOffsetY, capture.CircleWidth, capture.CircleHeight),
            capture.DebugImages);
    }

    public string GetTeamsStatus()
    {
        var avgColor = _captureService.CaptureAverageColor();
        var status = _detector.GetTeamsStatus(avgColor);

        Log.Debug("Sample AvgRGB=({R},{G},{B}) -> {Status}", avgColor.R, avgColor.G, avgColor.B, status);
        return status;
    }
}
