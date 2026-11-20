using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BBTools;

// What the caller knows about the current session. Everything here is shown to
// the player in report.json before it is shared; nothing is uploaded.
public sealed record BugReportContext(
    string Platform,
    string? SessionLogPath,
    bool Connected,
    bool Editing,
    string? ProcessName,
    string? LastStatus,
    string Language,
    IReadOnlyDictionary<string, string>? Details = null);

// Builds a ZIP with report.json plus the most recent session logs, so a bug
// report carries everything needed in one attachment. Saves, game files,
// libraries, settings and credentials are never included. The user's home
// folder and account name are replaced in every included text.
public static class BugReport
{
    public const int MaxLogs = 3;
    public const int MaxLogBytes = 2 * 1024 * 1024;
    private static readonly Regex SessionLogName = new(@"^BBTools-\d{8}T\d{13}Z-[0-9a-f]{32}\.log$", RegexOptions.Compiled);

    public static string DefaultFileName(DateTime now) => $"BBTools-bug-report-{AppInfo.DisplayVersion}-{now:yyyyMMdd-HHmmss}.zip";

    public static int Create(string destination, BugReportContext context, DateTime? createdUtc = null)
    {
        var logs = RecentLogs(context.SessionLogPath);
        var report = new
        {
            AppVersion = AppInfo.DisplayVersion,
            CreatedUtc = createdUtc ?? DateTime.UtcNow,
            context.Platform,
            OS = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.OSArchitecture.ToString(),
            Runtime = RuntimeInformation.FrameworkDescription,
            GameVersion = "CUSA03173 1.09",
            context.Connected,
            context.Editing,
            context.ProcessName,
            LastStatus = context.LastStatus == null ? null : Redact(context.LastStatus),
            context.Language,
            Details = context.Details?.ToDictionary(x => x.Key, x => Redact(x.Value)),
            IncludedLogs = logs.Select(Path.GetFileName).ToArray(),
        };
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                Add(zip, "report.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                foreach (var log in logs) Add(zip, "logs/" + Path.GetFileName(log), Redact(ReadTail(log)));
            }
            File.Move(temporary, destination, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return logs.Length;
    }

    // The current log first, then the newest earlier sessions from the same folder.
    internal static string[] RecentLogs(string? current)
    {
        if (string.IsNullOrEmpty(current)) return [];
        var folder = Path.GetDirectoryName(Path.GetFullPath(current));
        if (folder == null || !Directory.Exists(folder)) return [];
        var others = Directory.EnumerateFiles(folder, "BBTools-*.log")
            .Where(x => SessionLogName.IsMatch(Path.GetFileName(x)) && !Path.GetFullPath(x).Equals(Path.GetFullPath(current), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Path.GetFileName, StringComparer.Ordinal);
        return (File.Exists(current) ? new[] { current } : []).Concat(others).Take(MaxLogs).ToArray();
    }

    // The active session log stays open for writing, so share read/write access.
    internal static string ReadTail(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var skipped = Math.Max(0, stream.Length - MaxLogBytes);
        stream.Seek(skipped, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, new UTF8Encoding(false));
        var text = reader.ReadToEnd();
        return skipped > 0 ? $"[{skipped:N0} earlier bytes omitted]\n" + text[(text.IndexOf('\n') + 1)..] : text;
    }

    internal static string Redact(string text, string? home = null, string? user = null)
    {
        home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        user ??= Environment.UserName;
        if (!string.IsNullOrEmpty(home) && home.Length > 3)
        {
            text = text.Replace(home, "~", StringComparison.OrdinalIgnoreCase);
            text = text.Replace(home.Replace('\\', '/'), "~", StringComparison.OrdinalIgnoreCase);
        }
        if (!string.IsNullOrEmpty(user) && user.Length > 1)
            text = Regex.Replace(text, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(user)}(?![\p{{L}}\p{{N}}])", "[user]", RegexOptions.IgnoreCase);
        return text;
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(content);
    }
}
