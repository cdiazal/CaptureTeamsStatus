using Serilog;
using System.Text;
using System.Text.RegularExpressions;

// Reads the presence shown on the Teams taskbar badge, as the new Teams client (MSTeams) writes it to its own log:
// "TaskbarBadgeServiceLegacy:Work: SetBadge Setting badge: GlyphBadge{"doNotDistrb"}, overlay: No hay elementos, estado No molestar"
// The badge is the effective presence. "UserPresenceAction" lines are not: coming back from Away while in
// Do not disturb logs Away -> DoNotDisturb -> Available, and the presence stays Do not disturb.
public sealed class TeamsLogStatusSource : ITeamsStatusSource
{
    private static readonly Regex BadgeRegex = new(
        @"BadgeService\w*:Work: SetBadge Setting badge: (?:GlyphBadge\{""(?<glyph>[^""]*)""\}|[^,]*),.*, (?<state>[^,]+?)\s*$",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, string> GlyphStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["available"] = TeamStatusColors.Available,
        ["away"] = TeamStatusColors.Away,
        ["busy"] = TeamStatusColors.Busy,
        ["doNotDistrb"] = TeamStatusColors.DoNotDisturb
    };

    // The glyph is missing while there are unread notifications (NumericBadge), but the state text
    // is always there. It is localized, so it is matched by its ending ("estado No molestar").
    private static readonly (string Text, string Status)[] StateTexts =
    [
        ("Disponible", TeamStatusColors.Available),
        ("Available", TeamStatusColors.Available),
        ("Ausente", TeamStatusColors.Away),
        ("Away", TeamStatusColors.Away),
        ("Vuelvo enseguida", TeamStatusColors.Away),
        ("Be right back", TeamStatusColors.Away),
        ("Ocupado", TeamStatusColors.Busy),
        ("Busy", TeamStatusColors.Busy),
        ("En una llamada", TeamStatusColors.Busy),
        ("In a call", TeamStatusColors.Busy),
        ("En una reunión", TeamStatusColors.Busy),
        ("In a meeting", TeamStatusColors.Busy),
        ("No molestar", TeamStatusColors.DoNotDisturb),
        ("Do not disturb", TeamStatusColors.DoNotDisturb),
        ("Presentando", TeamStatusColors.DoNotDisturb),
        ("Presenting", TeamStatusColors.DoNotDisturb),
        ("Desconocido", TeamStatusColors.Unknown),
        ("Unknown", TeamStatusColors.Unknown),
        ("Desconectado", TeamStatusColors.Unknown),
        ("Offline", TeamStatusColors.Unknown)
    ];

    private readonly string _logDirectory;
    private readonly HashSet<string> _warnedStates = [];
    private string? _currentFile;
    private long _position;
    private string? _lastStatus;
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

        if (_lastStatus is null)
            WarnNoData("No Teams badge lines found in Teams logs at {LogDirectory}");

        return _lastStatus ?? TeamStatusColors.Unknown;
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
                .Select(ParseBadge)
                .LastOrDefault(b => b is not null);

            if (last is not null)
            {
                SetStatus(last.Value);
                return;
            }
        }
    }

    private void ProcessLines(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            var badge = ParseBadge(line);
            if (badge is not null)
                SetStatus(badge.Value);
        }
    }

    private void SetStatus((string Status, string Badge) badge)
    {
        if (badge.Status == _lastStatus)
            return;

        Log.Debug("Teams badge: {Badge} -> {Status}", badge.Badge, badge.Status);
        _lastStatus = badge.Status;
        _warnedNoData = false;
    }

    private void WarnNoData(string messageTemplate)
    {
        if (_warnedNoData)
            return;

        Log.Warning(messageTemplate, _logDirectory);
        _warnedNoData = true;
    }

    private (string Status, string Badge)? ParseBadge(string line)
    {
        var match = BadgeRegex.Match(line);
        if (!match.Success)
            return null;

        var glyph = match.Groups["glyph"].Value;
        if (GlyphStatuses.TryGetValue(glyph, out var glyphStatus))
            return (glyphStatus, glyph);

        var state = match.Groups["state"].Value;
        foreach (var (text, status) in StateTexts)
        {
            if (state.EndsWith(text, StringComparison.OrdinalIgnoreCase))
                return (status, state);
        }

        if (_warnedStates.Add(state))
            Log.Warning("Unmapped Teams badge state {State}", state);

        return (TeamStatusColors.Unknown, state);
    }

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
