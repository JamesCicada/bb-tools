using System.Diagnostics;
using System.Text.Json;

namespace BBTools.LinuxCore;

public sealed record LinuxCandidate(int Pid, string Name, string Identity)
{
    public string Label => $"{(LinuxProcessCatalog.Rank(Name)>0 ? "★ " : "")}{Name} · PID {Pid}{(Identity.Length==0 ? " · restricted" : "")}";
    public override string ToString() => Label;
}
public static class LinuxProcessCatalog
{
    public static int Rank(string name) => (name.Equals("bb-probe",StringComparison.OrdinalIgnoreCase)||name.Equals("bbprobe",StringComparison.OrdinalIgnoreCase)) ? 100 :
        name.Contains("bloodborne",StringComparison.OrdinalIgnoreCase) || name.Contains("bbport",StringComparison.OrdinalIgnoreCase) ? 80 :
        name.Contains("shadps4",StringComparison.OrdinalIgnoreCase) ? 60 : 0;
    public static LinuxCandidate[] List()
    {
        if(!OperatingSystem.IsLinux())return [];
        var items=new List<LinuxCandidate>();
        foreach(var folder in Directory.EnumerateDirectories("/proc"))
        {
            if(!int.TryParse(Path.GetFileName(folder),out var pid))continue;
            try
            {
                var name=File.ReadAllText(Path.Combine(folder,"comm")).TrimEnd('\n');
                string identity="";
                try{identity=LinuxProcessMemory.ParseIdentity(File.ReadAllText(Path.Combine(folder,"stat")));}
                catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){}
                items.Add(new(pid,name,identity));
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){}
        }
        return Filter(items,"");
    }
    public static LinuxCandidate[] Filter(IEnumerable<LinuxCandidate> items,string query)
    {
        var terms=query.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        return items.Where(x=>terms.All(t=>x.Label.Contains(t,StringComparison.OrdinalIgnoreCase)||t.Equals($"0x{x.Pid:X}",StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x=>Rank(x.Name)).ThenBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x.Pid).ToArray();
    }
    public static Process StartGame(string path,string argumentsJson)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("Launch is available on Linux only.");
        return Process.Start(CreateStartInfo(path,argumentsJson))??throw new IOException("The game launcher did not start.");
    }
    internal static ProcessStartInfo CreateStartInfo(string path,string argumentsJson)
    {
        if(!Path.IsPathFullyQualified(path)||!File.Exists(path))throw new ArgumentException("Choose an existing absolute path to the AppImage, launcher or run.sh.");
        var arguments=JsonSerializer.Deserialize<string[]>(argumentsJson)??throw new ArgumentException("Arguments must be a JSON array of strings, such as [\"--play\"].");
        if(arguments.Length>64||arguments.Any(x=>x==null||x.Length>4096||x.Contains('\0')))throw new ArgumentException("Invalid launcher arguments.");
        var info=new ProcessStartInfo(path){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(path)!};
        foreach(var argument in arguments)info.ArgumentList.Add(argument);
        return info;
    }
}
