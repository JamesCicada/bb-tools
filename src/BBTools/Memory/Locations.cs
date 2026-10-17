using System.IO;
using System.Text.Json;

namespace BBTools.Memory;

public sealed record SavedLocation(string Name, uint Block, float X, float Y, float Z, float Yaw)
{
    public string Label => $"{Name} · map {Block >> 24:D2}_{(Block >> 16) & 255:D2}";
    public override string ToString() => Label;
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 80 || Block == uint.MaxValue || Block == 0 ||
            new[] { X, Y, Z, Yaw }.Any(x => !float.IsFinite(x)) || new[] { X, Y, Z }.Any(x => Math.Abs(x) > 100000))
            throw new ArgumentException("The saved location is invalid.");
    }
    public byte[] Payload()
    {
        Validate();
        var payload = new byte[48];
        BitConverter.GetBytes(Block).CopyTo(payload, 0);
        BitConverter.GetBytes(X).CopyTo(payload, 16); BitConverter.GetBytes(Y).CopyTo(payload, 20); BitConverter.GetBytes(Z).CopyTo(payload, 24);
        BitConverter.GetBytes(Yaw).CopyTo(payload, 36);
        return payload;
    }
}

public sealed record LocationBook(SavedLocation[] Locations)
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BBTools", "locations.json");
    public static LocationBook Load(string? path = null)
    {
        try
        {
            var book = JsonSerializer.Deserialize<LocationBook>(File.ReadAllText(path ?? DefaultPath));
            if (book?.Locations == null || book.Locations.Length > 100) return new([]);
            foreach (var location in book.Locations) location.Validate();
            return book;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NullReferenceException) { return new([]); }
    }
    public void Save(string? path = null)
    {
        if (Locations.Length > 100) throw new ArgumentException("Keep at most 100 saved locations.");
        foreach (var location in Locations) location.Validate();
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

public sealed partial class PlayerSession
{
    public SavedLocation ReadLocation(string name)
    {
        ValidateImage();
        var player = ReadPlayer();
        var modules = memory.Pointer(player.Actor + 0x3B0);
        var transform = memory.Pointer(modules + 0x68);
        var bytes = memory.Read(transform + 0x1D4, 24);
        var location = new SavedLocation(name.Trim(), unchecked((uint)memory.Int32(player.Actor + 0x3F8)),
            BitConverter.ToSingle(bytes, 12), BitConverter.ToSingle(bytes, 16), BitConverter.ToSingle(bytes, 20), BitConverter.ToSingle(bytes, 0));
        location.Validate(); EnsureSamePlayer(player);
        return location;
    }
}
