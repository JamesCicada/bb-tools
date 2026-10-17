using System.Diagnostics;
using System.IO;

namespace BBTools.Memory;

public sealed record ProcessCandidate(int Id, string Name, long Started)
{
    public string Label => $"{Name}.exe · PID {Id}";
    public override string ToString() => Label;
}
public sealed record CheckedCandidate(ProcessCandidate Process, bool Loaded);

public static class ProcessDiscovery
{
    public static bool Matches(string name)
    {
        var normalized = name.Replace("-", "").Replace("_", "").Replace(" ", "");
        return new[] { "shadps4", "bloodborne", "bbport", "bbprobe" }.Any(fragment => normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }
    public static ProcessCandidate[] List()
    {
        var found = new List<ProcessCandidate>();
        foreach (var process in Process.GetProcesses())
        using (process)
        {
            try { if (Matches(process.ProcessName)) found.Add(new(process.Id, process.ProcessName, process.StartTime.ToUniversalTime().Ticks)); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        }
        return found.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).ToArray();
    }
    public static ProcessCandidate? Best(IEnumerable<CheckedCandidate> candidates) => candidates
        .OrderByDescending(x => x.Loaded)
        .ThenByDescending(x => x.Process.Name.Contains("probe", StringComparison.OrdinalIgnoreCase))
        .ThenByDescending(x => x.Process.Started).ThenBy(x => x.Process.Id)
        .Select(x => x.Process).FirstOrDefault();

    public static ProcessCandidate? FindBest(IEnumerable<ProcessCandidate> candidates, Func<bool> cancelled)
    {
        var supported = new List<CheckedCandidate>();
        foreach (var candidate in candidates)
        {
            if (cancelled()) return null;
            if (candidate.Name.Contains("shadps4", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var process = Process.GetProcessById(candidate.Id);
                if (process.StartTime.ToUniversalTime().Ticks != candidate.Started) continue;
                using var memory = new ProcessMemory(candidate.Id, writable: false);
                var image = PlayerSession.FindImage(memory, memory.AllocationBases());
                var session = new PlayerSession(memory, image);
                var loaded = false;
                try { session.ReadCharacter(); loaded = true; } catch (IOException) { }
                supported.Add(new(candidate, loaded));
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
        }
        return Best(supported);
    }
}
