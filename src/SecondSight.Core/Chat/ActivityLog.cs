using System.Globalization;

namespace SecondSight.Core.Chat;

/// <summary>
/// Append-only daily log of what the bot heard from its owner and everything it said or did,
/// so the owner can review it. One file per day: activity-YYYY-MM-DD.log.
/// </summary>
public sealed class ActivityLog
{
    private readonly string _directory;
    private readonly TimeProvider _time;
    private readonly object _lock = new();

    public ActivityLog(string? directory = null, TimeProvider? time = null)
    {
        _directory = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory : directory;
        _time = time ?? TimeProvider.System;
    }

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SecondSight", "logs");

    public string Directory => _directory;

    public string CurrentFile => Path.Combine(_directory, $"activity-{_time.GetLocalNow():yyyy-MM-dd}.log");

    public void Write(string kind, string text)
    {
        var line = string.Create(CultureInfo.InvariantCulture,
            $"{_time.GetLocalNow():yyyy-MM-dd HH:mm:ss zzz}\t{kind}\t{text.Replace('\n', ' ').Replace('\r', ' ')}{Environment.NewLine}");
        try
        {
            lock (_lock)
            {
                System.IO.Directory.CreateDirectory(_directory);
                File.AppendAllText(CurrentFile, line);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
