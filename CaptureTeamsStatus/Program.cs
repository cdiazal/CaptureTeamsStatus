using Microsoft.Extensions.Configuration;
using Serilog;
using System.Drawing;

class Program
{
    static async Task Main(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(config)
            .CreateLogger();

        Log.Information("Starting Teams status capture. Press Ctrl+C to stop.");

        var settings = config.Get<AppSettings>() ?? new AppSettings();
                
        var detector = new StatusDetector();
        var captureService = new ScreenCaptureService(
            new Rectangle(settings.Capture.X, settings.Capture.Y, settings.Capture.Width, settings.Capture.Height),
            new Rectangle(settings.Capture.CircleOffsetX, settings.Capture.CircleOffsetY, settings.Capture.CircleWidth, settings.Capture.CircleHeight),
            settings.Capture.DebugImages);

        var presenceClient = new PresenceClient(settings.ApiBaseUrl);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
            Log.Warning("Cancellation requested. Exiting...");
        };

        string? lastPublished = null;
        var pollInterval = TimeSpan.FromMilliseconds(settings.Capture.PollIntervalMs);
        var timer = new PeriodicTimer(pollInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cts.Token))
            {
                try
                {
                    var avgColor = captureService.CaptureAverageColor();
                    var status = detector.GetTeamsStatus(avgColor);

                    Log.Debug("Sample AvgRGB=({R},{G},{B}) -> {Status}", avgColor.R, avgColor.G, avgColor.B, status);

                    if (status != lastPublished && status != TeamStatusColors.Unknown)
                    {
                        Log.Information("Status change: {Prev} -> {Next}", lastPublished ?? "(none)", status);
                        if (status == TeamStatusColors.DoNotDisturb)
                            await presenceClient.SetPresentingAsync(cts.Token);
                        else
                            await presenceClient.SetPresenceColorAsync(status, cts.Token);

                        lastPublished = status;
                    }
                    else
                    {
                        Log.Debug("No publish. LastPublished={Status}", lastPublished ?? "(none)");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Loop error");
                }

                if (cts.IsCancellationRequested)
                    break;
            }
        }
        finally
        {
            Log.Information("Stopped.");
            Log.CloseAndFlush();
        }
    }
}