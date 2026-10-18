using System.IO;
using System.Text.Json;

namespace BBTools.Memory;

public sealed record SavedLocation(string Name, uint Block, float X, float Y, float Z, float Yaw, string Group = "General", DungeonBookmark? Dungeon = null)
{
    public string Label => $"{Group} · {Name} · map {Block >> 24:D2}_{(Block >> 16) & 255:D2}";
    public override string ToString() => Label;
    public void Validate()
    {
        UserLibrary.ValidateName(Group);Dungeon?.Validate();
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
        if ((location.Block >> 24) == 29)location=location with{Dungeon=ReadDungeonBookmark()};
        location.Validate(); EnsureSamePlayer(player);
        return location;
    }
}

public sealed record DungeonBookmark(string Session,ulong Character,int Altar,uint MapUid,string StateHash)
{
    public void Validate(){if(string.IsNullOrEmpty(Session)||string.IsNullOrEmpty(StateHash)||Session.Length!=32||!Session.All(Uri.IsHexDigit)||Character<0x10000||Altar is <1 or >6||MapUid==0||StateHash.Length!=64||!StateHash.All(Uri.IsHexDigit))throw new ArgumentException("Invalid chalice bookmark identity.");}
}
public sealed partial class PlayerSession
{
    private readonly string bookmarkSession=Guid.NewGuid().ToString("N");
    private DungeonBookmark ReadDungeonBookmark()
    {
        var character=ReadCharacter();var flags=new EventFlagStore(memory,memory.Pointer(imageBase+0x553B100));var selected=Enumerable.Range(0,7).Where(i=>flags.Read((uint)(9020+i))).ToArray();
        if(selected.Length!=1||selected[0] is <1 or >6)throw new IOException("An ordinary selected chalice altar is required for coordinate bookmarks.");
        var bank=selected[0];var owner=memory.Pointer(imageBase+0x553E9E8);if(memory.Int32(owner)!=7)throw new IOException("Chalice altar manager is unavailable.");
        var gate=memory.Pointer(memory.Pointer(owner+8)+(ulong)bank*8);if(memory.Int32(gate+0x100)!=bank||memory.Pointer(gate)!=imageBase+0x5336570)throw new IOException("Chalice gate identity failed validation.");
        var uid=unchecked((uint)memory.Int32(gate+0x108+0xE0));if(uid==0||memory.Int32(gate+0x108+0x25C) is not (4 or 5))throw new IOException("Chalice instance is not ready.");
        var state=new byte[0x600/8];var start=94000000u+(uint)bank*100000;
        for(var i=0;i<0x600;i++)if(flags.Read(start+(uint)i))state[i/8]|=(byte)(1<<(i%8));
        if(ReadCharacter().Address!=character.Address||unchecked((uint)memory.Int32(gate+0x108+0xE0))!=uid)throw new IOException("Chalice context changed while reading its identity.");
        return new(bookmarkSession,character.Address,bank,uid,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(state)));
    }
    internal void ValidateLocationContext(SavedLocation target,SavedLocation current)
    {
        if(target.Block!=current.Block)throw new IOException("Travel to this location's map using a lamp first, then restore it.");
        if(target.Block>>24==29 && (target.Dungeon==null||target.Dungeon!=current.Dungeon))throw new IOException("This chalice bookmark belongs to another connection, character, altar, dungeon or progress state. Save a fresh bookmark in the current instance.");
        if(target.Block>>24!=29&&target.Dungeon!=null)throw new IOException("Fixed-map bookmark contains an invalid dungeon identity.");
    }
}
