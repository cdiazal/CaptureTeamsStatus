using Microsoft.Extensions.Configuration;
using Serilog;

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
                
        Log.Information("Status source: {Source}", settings.Source.ToString());
        ITeamsStatusSource statusSource = settings.Source == StatusSource.Screen
            ? new ScreenStatusSource(settings.Capture)
            : new TeamsLogStatusSource(settings.TeamsLogDirectory);

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
                    var status = statusSource.GetTeamsStatus();

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