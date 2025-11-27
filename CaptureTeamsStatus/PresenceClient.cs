using Serilog;

public sealed class PresenceClient
{
    private readonly HttpClient _http;

    public PresenceClient(string baseUrl)
    {
        _http = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = static (_, _, _, _) => true 
        })
        {
            BaseAddress = new Uri(baseUrl)
        };
    }

    public async Task SetPresenceColorAsync(string color, CancellationToken ct)
    {
        var url = $"TeamsPresence/SetPresenceColor?color={Uri.EscapeDataString(color)}";
        Log.Information("Calling {Url}", _http.BaseAddress + url);
        try
        {
            using var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            Log.Information("Presence color updated.");
        }
        catch (TaskCanceledException)
        {
            Log.Warning("HTTP request canceled.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating presence color {Color}", color);
        }
    }

    public async Task SetPresentingAsync(CancellationToken ct)
    {
        const string url = "TeamsPresence/Presenting";
        Log.Information("Calling {Url}", _http.BaseAddress + url);
        try
        {
            using var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            Log.Information("Presenting status updated.");
        }
        catch (TaskCanceledException)
        {
            Log.Warning("HTTP request canceled.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating presenting");
        }
    }

    public Task DisposeAsync()
    {
        _http.Dispose();
        return Task.CompletedTask;
    }
}