public sealed class CaptureSettings
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int CircleOffsetX { get; init; }
    public int CircleOffsetY { get; init; }
    public int CircleWidth { get; init; }
    public int CircleHeight { get; init; }
    public int PollIntervalMs { get; init; } = 5000;
    public bool DebugImages { get; init; } = false;
}