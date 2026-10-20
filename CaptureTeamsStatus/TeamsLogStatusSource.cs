using Serilog;
using System.Text;
using System.Text.RegularExpressions;

// Reads the presence that the new Teams client (MSTeams) writes to its own log:
// "UserPresenceAction: {cloud_context: https://teams.microsoft.com, availability: Busy}"
public sealed class TeamsLogStatusSource : ITeamsStatusSource
{
    private static readonly Regex PresenceRegex = new(@"UserPresenceAction: \{[^}]*availability: (\w+)\}", RegexOptions.Compiled);

    private readonly string _logDirectory;
    private string? _currentFile;
    private long _position;
    private string? _lastAvailability;
    private bool _warnedNoData;

    public TeamsLogStatusSource(string? logDirectory)
    {
        _logDirectory = string.IsNullOrWhiteSpace(logDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\Logs")
            : Environment.ExpandEnvironmentVariables(logDirectory);

        Log.Information("Reading Teams presence from {LogDirectory}", _logDirectory);
    }

    public string GetTeamsStatus()
    {
        var files = GetLogFilesNewestFirst();
        if (files.Length == 0)
        {
            WarnNoData("No Teams log files found in {LogDirectory}");
            return TeamStatusColors.Unknown;
        }

        var newest = files[0];
        if (newest != _currentFile)
        {
            if (_currentFile is null)
                SeedFromOlderLogs(files.Skip(1));
            else if (File.Exists(_currentFile))
                ProcessLines(ReadCompleteLines(_currentFile, ref _position));

            Log.Debug("Following Teams log {File}", Path.GetFileName(newest));
            _currentFile = newest;
            _position = 0;
        }

        ProcessLines(ReadCompleteLines(_currentFile, ref _position));

        if (_lastAvailability is null)
            WarnNoData("No presence lines found in Teams logs at {LogDirectory}");

        return MapAvailability(_lastAvailability);
    }

    private string[] GetLogFilesNewestFirst()
    {
        if (!Directory.Exists(_logDirectory))
            return [];

        return new DirectoryInfo(_logDirectory)
            .GetFiles("MSTeams_*.log")
            .OrderByDescending(f => f.CreationTimeUtc)
            .ThenByDescending(f => f.Name, StringComparer.Ordinal)
            .Select(f => f.FullName)
            .ToArray();
    }

    private void SeedFromOlderLogs(IEnumerable<string> olderFiles)
    {
        foreach (var file in olderFiles)
        {
            long position = 0;
            var last = ReadCompleteLines(file, ref position)
                .Select(ParseAvailability)
                .LastOrDefault(a => a is not null);

            if (last is not null)
            {
                SetAvailability(last);
                return;
            }
        }
    }

    private void ProcessLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var availability = ParseAvailability(line);
            if (availability is not null)
                SetAvailability(availability);
        }
    }

    private void SetAvailability(string availability)
    {
        if (availability == _lastAvailability)
            return;

        Log.Debug("Teams availability: {Availability}", availability);
        if (MapAvailability(availability) == TeamStatusColors.Unknown && availability is not ("Offline" or "PresenceUnknown"))
            Log.Warning("Unmapped Teams availability {Availability}", availability);

        _lastAvailability = availability;
        _warnedNoData = false;
    }

    private void WarnNoData(string messageTemplate)
    {
        if (_warnedNoData)
            return;

        Log.Warning(messageTemplate, _logDirectory);
        _warnedNoData = true;
    }

    private static string? ParseAvailability(string line)
    {
        var match = PresenceRegex.Match(line);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string MapAvailability(string? availability) => availability switch
    {
        "Available" or "AvailableIdle" => TeamStatusColors.Available,
        "Away" or "BeRightBack" => TeamStatusColors.Away,
        "Busy" or "BusyIdle" => TeamStatusColors.Busy,
        "DoNotDisturb" => TeamStatusColors.DoNotDisturb,
        _ => TeamStatusColors.Unknown
    };

    // Returns only full lines, so a line Teams is still writing is picked up on the next read.
    private static string[] ReadCompleteLines(string path, ref long position)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < position)
                position = 0;

            if (stream.Length == position)
                return [];

            stream.Seek(position, SeekOrigin.Begin);
            var buffer = new byte[stream.Length - position];
            var read = stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
            if (read == 0)
                return [];

            var lastNewLine = Array.LastIndexOf(buffer, (byte)'\n', read - 1);
            if (lastNewLine < 0)
                return [];

            position += lastNewLine + 1;
            return Encoding.UTF8.GetString(buffer, 0, lastNewLine + 1).Split('\n');
        }
        catch (FileNotFoundException)
        {
            return [];
        }
    }
}
