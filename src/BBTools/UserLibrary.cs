using System.IO;
using System.Text.Json;
using BBTools.Memory;
using Microsoft.Data.Sqlite;

namespace BBTools;

public sealed record PracticePreset(string Name, bool KeepHealth, bool Stamina, string[] Flags, float Damage, float Echoes, float Fov = 43, bool OverrideFov = false, AppSettings? Preferences = null, bool KeepDurability = false, bool AutoRepair = false, int RepairThreshold = 30, bool TuneCamera = false, float CameraDistance = 1, float CameraHeight = 1, string Group = "General", bool KeepStatusEmpty = false)
{
    public override string ToString() => $"{Group} · {Name}";
    public void Validate()
    {
        UserLibrary.ValidateName(Name);UserLibrary.ValidateName(Group);
        Preferences?.Validate();
        if (!float.IsFinite(CameraDistance) || !float.IsFinite(CameraHeight) || CameraDistance is < .5f or >2 || CameraHeight is < .5f or >2) throw new ArgumentException("Invalid preset camera tuning.");
        if (RepairThreshold is < 1 or > 100) throw new ArgumentException("Repair threshold must be 1–100.");
        if (!float.IsFinite(Fov) || Fov is < 38 or > 120) throw new ArgumentException("Preset FOV must be between 38 and 120.");
        if (Flags == null || Flags.Length > 7 || Flags.Distinct().Count() != Flags.Length ||
            Flags.Any(x => !Enum.GetNames<PracticeFlag>().Contains(x)) ||
            !float.IsFinite(Damage) || !float.IsFinite(Echoes) || Damage is < .1f or > 100 || Echoes is < .1f or > 100)
            throw new ArgumentException("Invalid practice preset.");
    }
}

public sealed partial class UserLibrary
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BBTools", "library.db");
    private readonly string path;
    public UserLibrary(string? path = null, string? legacyPath = null)
    {
        this.path = path ?? DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(this.path))!);
        using var db = Open();
        using var tx = db.BeginTransaction();
        using var command = db.CreateCommand(); command.Transaction = tx;
        command.CommandText = "PRAGMA user_version";
        var version = Convert.ToInt32(command.ExecuteScalar());
        if (version > 1) throw new IOException("This library was created by a newer BB Tools version.");
        command.CommandText = "CREATE TABLE IF NOT EXISTS entries(kind TEXT NOT NULL, name TEXT COLLATE NOCASE NOT NULL, data TEXT NOT NULL, PRIMARY KEY(kind,name)); CREATE TABLE IF NOT EXISTS metadata(key TEXT PRIMARY KEY, value TEXT); PRAGMA user_version=1;";
        command.ExecuteNonQuery();
        command.CommandText = "SELECT COUNT(*) FROM metadata WHERE key='locations-migrated'";
        if (Convert.ToInt32(command.ExecuteScalar()) == 0)
        {
            var legacy = legacyPath ?? LocationBook.DefaultPath;
            if (File.Exists(legacy))
            {
                var book = JsonSerializer.Deserialize<LocationBook>(File.ReadAllText(legacy)) ?? throw new IOException("Invalid legacy location file; original retained.");
                if (book.Locations == null || book.Locations.Length > 100) throw new IOException("Invalid legacy location collection; original retained.");
                foreach (var location in book.Locations)
                {
                    location.Validate();
                    command.CommandText = "INSERT OR IGNORE INTO entries VALUES('location',$name,$data)";
                    command.Parameters.Clear(); command.Parameters.AddWithValue("$name",location.Name); command.Parameters.AddWithValue("$data",JsonSerializer.Serialize(location)); command.ExecuteNonQuery();
                }
            }
            command.Parameters.Clear(); command.CommandText = "INSERT INTO metadata VALUES('locations-migrated','1')"; command.ExecuteNonQuery();
        }
        tx.Commit();
    }
    private SqliteConnection Open()
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        try { db.Open(); return db; } catch { db.Dispose(); throw; }
    }
    public static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name != name.Trim()) throw new ArgumentException("Use a name of 1–80 characters without surrounding spaces.");
    }
    private T[] Read<T>(string kind)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT data FROM entries WHERE kind=$kind ORDER BY name COLLATE NOCASE"; cmd.Parameters.AddWithValue("$kind",kind);
        using var reader = cmd.ExecuteReader(); var result = new List<T>();
        while(reader.Read()) result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0)) ?? throw new IOException("Invalid saved library entry."));
        return result.ToArray();
    }
    public SavedLocation[] Locations() { var all = Read<SavedLocation>("location"); foreach(var x in all) x.Validate(); return all; }
    public PracticePreset[] Presets() { var all = Read<PracticePreset>("preset"); foreach(var x in all) x.Validate(); return all; }
    public void Save(SavedLocation location) { location.Validate(); ValidateName(location.Name); Save("location",location.Name,location); }
    public void Save(PracticePreset preset) { preset.Validate(); Save("preset",preset.Name,preset); }
    private void Save<T>(string kind,string name,T value)
    {
        using var db = Open(); using var cmd = db.CreateCommand();
        cmd.CommandText = "INSERT INTO entries VALUES($kind,$name,$data) ON CONFLICT(kind,name) DO UPDATE SET name=excluded.name,data=excluded.data";
        cmd.Parameters.AddWithValue("$kind",kind); cmd.Parameters.AddWithValue("$name",name); cmd.Parameters.AddWithValue("$data",JsonSerializer.Serialize(value)); cmd.ExecuteNonQuery();
    }
    public void Delete(string kind,string name)
    {
        if (kind is not ("location" or "preset" or "favorite")) throw new ArgumentException("Unknown library category.");
        using var db = Open(); using var cmd = db.CreateCommand(); cmd.CommandText="DELETE FROM entries WHERE kind=$kind AND name=$name";
        cmd.Parameters.AddWithValue("$kind",kind); cmd.Parameters.AddWithValue("$name",name); cmd.ExecuteNonQuery();
    }
}
