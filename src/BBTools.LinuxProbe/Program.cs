using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using BBTools.Memory;
using BBTools.LinuxCore;

namespace BBTools.LinuxProbe;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args.SequenceEqual(new[] { "--help" }))
            {
                Console.WriteLine("BB Tools Linux connection probe (development, read-only)\nTarget: native bbport, Linux x64, game 1.09\n--list       List processes; likely bbport runtimes appear first\n--diagnose   Print platform/access-policy metadata without reading a game\n--pid <PID>  Validate the game image and read loaded player data as JSON");
                return 0;
            }
            if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new PlatformNotSupportedException("Use this probe on native Linux x64.");
            if(args.SequenceEqual(new[]{"--diagnose"}))
            {
                Console.WriteLine(JsonSerializer.Serialize(new{Mode="read-only",OS=RuntimeInformation.OSDescription,Architecture=RuntimeInformation.ProcessArchitecture.ToString(),PtraceScope=LinuxPermissions.Scope(),Flatpak=File.Exists("/.flatpak-info"),X11=!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")),Wayland=!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))},new JsonSerializerOptions{WriteIndented=true}));return 0;
            }
            if (args.SequenceEqual(new[] { "--list" }))
            {
                var processes = new List<(int Pid, string Name, int Rank)>();
                foreach (var folder in Directory.EnumerateDirectories("/proc"))
                {
                    if (!int.TryParse(Path.GetFileName(folder), out var id)) continue;
                    try
                    {
                        var name = File.ReadAllText(Path.Combine(folder, "comm")).TrimEnd('\n');
                        processes.Add((id, name, Rank(name)));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                }
                foreach (var p in processes.OrderByDescending(p => p.Rank).ThenBy(p => p.Name).ThenBy(p => p.Pid))
                    Console.WriteLine($"{(p.Rank > 0 ? "*" : " ")} {p.Pid,8}  {p.Name}");
                return 0;
            }
            if (args.Length != 2 || args[0] != "--pid" || !int.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var pid) || pid <= 0)
                throw new ArgumentException("Use --list or --pid <positive PID>. The probe has no write commands.");
            var memory = new LinuxProcessMemory(pid);
            var image = LinuxImageLocator.Find(memory, memory.Maps(), TimeSpan.FromSeconds(20));
            var session = new PlayerSession(memory, image);
            session.ValidateImage();
            var player = session.ReadPlayer();
            var character = session.ReadCharacter();
            if (session.ReadPlayer().Actor != player.Actor || session.ReadCharacter().Address != character.Address)
                throw new IOException("Player changed during inspection. Try again after loading.");
            session.ValidateImage();
            Console.WriteLine(JsonSerializer.Serialize(new { Mode = "read-only", Target = "native bbport", PID = pid,
                OS = RuntimeInformation.OSDescription, Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                ImageBase = $"0x{image:X}", player.Health, player.MaxHealth, player.Stamina, player.MaxStamina,
                character.Level, character.Values }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException or NotSupportedException)
        {
            Console.Error.WriteLine($"Probe failed: {ex.Message}");
            return 1;
        }
    }
    internal static int Rank(string name) => name.Equals("bb-probe", StringComparison.OrdinalIgnoreCase) ? 100 :
        name.Contains("bloodborne", StringComparison.OrdinalIgnoreCase) || name.Contains("bbport", StringComparison.OrdinalIgnoreCase) ? 80 : 0;
}
