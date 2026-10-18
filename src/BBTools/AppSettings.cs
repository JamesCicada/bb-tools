using System.IO;
using System.Text.Json;

namespace BBTools;

public sealed record AppSettings(bool AutoConnect = false, bool StartupFovEnabled = false, float StartupFov = 43, string? StartupPresetName = null, HotkeyPreferences? Hotkeys = null)
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BBTools", "settings.json");
    public void Validate()
    {
        Hotkeys?.Validate();
        if (!float.IsFinite(StartupFov) || StartupFov is < 38 or > 120) throw new ArgumentException("Startup FOV must be between 38 and 120.");
        if (StartupPresetName != null) UserLibrary.ValidateName(StartupPresetName);
    }
    public static AppSettings Load(string? path = null)
    {
        try { var result = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path ?? DefaultPath)) ?? new(); result.Validate(); return result; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return new(); }
    }
    public void Save(string? path = null)
    {
        Validate();
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
