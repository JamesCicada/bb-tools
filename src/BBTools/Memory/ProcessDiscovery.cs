using System.Diagnostics;
using System.IO;

namespace BBTools.Memory;

public sealed record ProcessCandidate(int Id, string Name, long Started)
{
    public string Label => $"{(ProcessDiscovery.Matches(Name) ? "★ " : "")}{Name}.exe · PID {Id}{(Started == 0 ? " · restricted" : "")}";
    public override string ToString() => Label;
}
public sealed record CheckedCandidate(ProcessCandidate Process, bool Loaded);

public static class ProcessDiscovery
{
    private static string Normalize(string name) => name.Replace("-", "").Replace("_", "").Replace(" ", "").ToLowerInvariant();
    public static int Likelihood(string name)
    {
        var normalized = Normalize(name);
        if (normalized is "bbprobe" or "bbprobe.exe") return 100;
        if (normalized.Contains("bbprobe",StringComparison.Ordinal)) return 90;
        if (normalized.Contains("bloodborne",StringComparison.Ordinal)) return 80;
        if (normalized.Contains("bbport",StringComparison.Ordinal)) return 70;
        if (normalized.Contains("shadps4",StringComparison.Ordinal)) return 60;
        return 0;
    }
    public static bool Matches(string name) => Likelihood(name) > 0;
    public static ProcessCandidate[] Filter(IEnumerable<ProcessCandidate> candidates, string filter = "")
    {
        var terms = filter.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return candidates.Where(x => terms.All(term =>
                x.Label.Contains(term,StringComparison.OrdinalIgnoreCase) ||
                $"0x{x.Id:X}".Equals(term,StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => Likelihood(x.Name)).ThenBy(x => x.Name,StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).ToArray();
    }
    public static ProcessCandidate[] List()
    {
        var found = new List<ProcessCandidate>();
        foreach (var process in Process.GetProcesses())
        using (process)
        {
            try
            {
                var name=process.ProcessName;
                long started=0;
                try { started=process.StartTime.ToUniversalTime().Ticks; }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
                found.Add(new(process.Id,name,started));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        return Filter(found);
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
            if (!Matches(candidate.Name) || candidate.Started == 0) continue;
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
