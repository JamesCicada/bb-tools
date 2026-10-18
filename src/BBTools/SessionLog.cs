using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BBTools;

public sealed class SessionLog : IDisposable
{
    private readonly object gate = new();
    private readonly StreamWriter writer;
    private readonly Dictionary<string, DateTime> errors = new();
    private bool disposed;
    public string FilePath { get; }
    public string? Failure { get; private set; }
    public SessionLog(string appFolder)
    {
        var folder = Path.Combine(Path.GetFullPath(appFolder), "logs");
        Directory.CreateDirectory(folder);
        FilePath = Path.Combine(folder, $"BBTools-{DateTime.UtcNow:yyyyMMddTHHmmssfffffffZ}-{Guid.NewGuid():N}.log");
        writer = new StreamWriter(new FileStream(FilePath,FileMode.CreateNew,FileAccess.Write,FileShare.Read | FileShare.Delete),new UTF8Encoding(false)) { AutoFlush=true };
        Write("INFO",$"Session started | BB Tools {AppInfo.DisplayVersion} | PID {Environment.ProcessId} | OS {Environment.OSVersion} | .NET {Environment.Version}");
        // Delete only files matching our exact session filename, ordered by session start.
        foreach(var old in Directory.EnumerateFiles(folder,"BBTools-*.log")
            .Where(x=>Regex.IsMatch(Path.GetFileName(x),@"^BBTools-\d{8}T\d{13}Z-[0-9a-f]{32}\.log$"))
            .OrderByDescending(Path.GetFileName,StringComparer.Ordinal).Skip(5))
        {
            try { File.Delete(old); }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { Write("WARN",$"Could not remove old session log: {ex.Message}"); }
        }
    }
    public void Write(string level,string message)
    {
        lock(gate)
        {
            if(disposed || Failure != null) return;
            try { writer.WriteLine($"{DateTime.UtcNow:O} [{level}] {message.Replace("\r", "\\r").Replace("\n", "\\n")}"); }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { Failure=ex.Message; }
        }
    }
    public void Error(Exception error,string context)
    {
        lock(gate)
        {
            var key=context+"|"+error;
            var now=DateTime.UtcNow;
            // Loading-screen polling must not flood the session log with the same failure.
            if(errors.TryGetValue(key,out var last) && now-last < TimeSpan.FromSeconds(30)) return;
            if(errors.Count >= 256) errors.Clear();
            errors[key]=now;
            Write("ERROR",$"{context}: {error}");
        }
    }
    public void Dispose()
    {
        lock(gate)
        {
            if(disposed) return;
            Write("INFO","Session ended"); disposed=true;
            try { writer.Dispose(); } catch(IOException ex) { Failure=ex.Message; }
        }
    }
}
