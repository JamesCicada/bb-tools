using System.IO;
using System.Text;

namespace BBTools.Memory;

public sealed record GemEffect(int Id, int Rank, int Category, int SpecialEffect, int AttackSpecialEffect = -1);
public sealed record GemTemplate(int Id, uint NameOffset);
public sealed record GemCatalog(GemTemplate[] Templates, GemEffect[] Effects);
public sealed record GemSpec(int Source, int Shape, int[] Effects)
{
    public void Validate()
    {
        if (Source is < 1 or > 0x0FFFFFFE || Shape is not (1 or 2 or 4 or 8 or 63) || Effects == null || Effects.Length != 6 ||
            Effects.Any(x => x < -1 || x == 0) || Effects.All(x => x == -1))
            throw new ArgumentException("Choose a blood-gem template, a valid shape and one to six effects. Empty effect slots use -1.");
    }
}
public sealed record InventoryGem(uint Handle, GemSpec Spec)
{
    public string Label => $"{Handle:X8} · {Spec.Source} · {string.Join(", ", Spec.Effects.Where(x => x != -1))}";
}

// Runtime instance offsets differ from serialized save offsets. Only native
// creation is used for writes; reading a gem into the editor never changes it.
public sealed class BloodGems(IMemory memory, PlayerSession session)
{
    internal sealed record Table(ulong File, Dictionary<int, ulong> Rows);
    internal Table Resolve(bool effects)
    {
        var repo = memory.Pointer(session.ImageBase + 0x5540340);
        var slot = repo + 0x70 + (effects ? 43UL : 42UL) * 0x48;
        if (memory.Int32(slot) != 1) throw new IOException("A unique blood-gem parameter table is not loaded.");
        var cap = memory.Pointer(slot + 8); var name = effects ? "GemeffectParam\0" : "GemGenParam\0";
        if (Encoding.Unicode.GetString(memory.Read(memory.Pointer(cap + 0x18), name.Length * 2)) != name)
            throw new IOException("Blood-gem parameter resource name failed validation.");
        var file = memory.Pointer(memory.Pointer(cap + 0x70) + 0x70);
        var header = memory.Read(file, 0x40);
        var type = effects ? "GEMEFFECT_PARAM_ST\0" : "GEM_GEN_PARAM_ST\0";
        if (!header.AsSpan(12, type.Length).SequenceEqual(Encoding.ASCII.GetBytes(type))) throw new IOException("Blood-gem parameter type failed validation.");
        var count = BitConverter.ToUInt16(header, 10);
        if (count is < 1 or > 30000) throw new IOException("Invalid blood-gem row count.");
        var wide = header[0x2D] > 3 && (header[0x2E] & 2) != 0;
        var start = header[0x2D] == 2 ? 0x34 : 0x40; var stride = wide ? 24 : 12;
        return new(file, ParseRows(memory.Read(file + (ulong)start, count * stride), count, start, stride, file, effects ? 0x24 : 0x90));
    }
    internal static Dictionary<int, ulong> ParseRows(byte[] records, int count, int start, int stride, ulong file, int rowSize)
    {
        if (count is < 1 or > 30000 || stride is not (12 or 24) || records.Length != count * stride) throw new IOException("Invalid blood-gem records.");
        var result = new Dictionary<int, ulong>();
        for (var i = 0; i < count; i++)
        {
            var id = BitConverter.ToInt32(records, i * stride);
            var offset = stride == 24 ? BitConverter.ToUInt64(records, i * stride + 8) : BitConverter.ToUInt32(records, i * stride + 4);
            if (id < 0 || id > 0x0FFFFFFE || offset < (ulong)(start + count * stride) || offset > 0x2000000 || offset % 4 != 0 || !result.TryAdd(id, checked(file + offset)))
                throw new IOException("Invalid blood-gem parameter record.");
        }
        var addresses = result.Values.Order().ToArray();
        if (addresses.Zip(addresses.Skip(1)).Any(x => x.Second - x.First < (ulong)rowSize)) throw new IOException("Blood-gem parameter rows overlap.");
        return result;
    }
    public GemCatalog ReadCatalog()
    {
        session.ValidateImage(); session.ReadPlayer();
        var generators = Resolve(false); var effects = Resolve(true);
        // Bulk reads keep catalog refresh off the per-frame polling path.
        byte[] Block(Table t, int size) => memory.Read(t.File, checked((int)(t.Rows.Values.Max() - t.File) + size));
        var gen = Block(generators, 0x90); var fx = Block(effects, 0x24);
        var templates = generators.Rows.Where(x => x.Key > 0 && (gen[checked((int)(x.Value - generators.File)) + 0x8C] & 3) == 2)
            .Select(x => new GemTemplate(x.Key, BitConverter.ToUInt32(gen, checked((int)(x.Value - generators.File)) + 8))).OrderBy(x => x.Id).ToArray();
        var effectRows = effects.Rows.Select(x =>
        {
            var at = checked((int)(x.Value - effects.File));
            return new GemEffect(x.Key, BitConverter.ToInt32(fx, at + 8), BitConverter.ToInt32(fx, at + 4), BitConverter.ToInt32(fx, at), BitConverter.ToInt32(fx, at + 0x20));
        }).Where(x => x.Id > 0 && (x.SpecialEffect >= 0 || x.AttackSpecialEffect >= 0)).OrderBy(x => x.Id).ToArray();
        if (Resolve(false).File != generators.File || Resolve(true).File != effects.File) throw new IOException("Blood-gem tables changed during refresh.");
        return new(templates, effectRows);
    }
    internal byte[] Prepare(GemSpec spec)
    {
        spec.Validate(); session.ValidateImage(); session.ReadCharacter();
        var generators = Resolve(false); var effects = Resolve(true);
        if (!generators.Rows.TryGetValue(spec.Source, out var generator) || (memory.Read(generator + 0x8C, 1)[0] & 3) != 2)
            throw new ArgumentException("The template must be a blood gem, not a Caryll rune.");
        var payload = new byte[176];
        void Int(int at, int value) => BitConverter.GetBytes(value).CopyTo(payload, at);
        void Ptr(int at, ulong value) => BitConverter.GetBytes(value).CopyTo(payload, at);
        Int(0, 1); Int(8, unchecked((int)(0x80000000 | (uint)spec.Source))); Int(12, 1); Int(16, -1);
        Int(32, spec.Source); Ptr(40, generator); Int(48, spec.Shape);
        for (var i = 0; i < 6; i++)
        {
            var id = spec.Effects[i]; Int(56 + i * 16, id);
            if (id != -1)
            {
                if (!effects.Rows.TryGetValue(id, out var row)) throw new ArgumentException($"Blood-gem effect ID {id} does not exist in the loaded game.");
                if (memory.Int32(row) < 0 && memory.Int32(row + 0x20) < 0) throw new ArgumentException($"Blood-gem effect ID {id} has no native property. Choose a functional effect or use -1 for empty.");
                Ptr(64 + i * 16, row);
            }
        }
        return payload;
    }
    public InventoryGem[] ReadInventory()
    {
        session.ValidateImage(); var character = session.ReadCharacter();
        var inventory = character.Address + 0x328;
        var first = unchecked((uint)memory.Int32(inventory + 0x24));
        var last = unchecked((uint)memory.Int32(inventory + 0x88));
        if (last == uint.MaxValue) return [];
        if (first > 100000 || last >= 100000) throw new IOException("Invalid blood-gem inventory bounds.");
        var manager = memory.Pointer(session.ImageBase + 0x553E990);
        var result = new List<InventoryGem>();
        var count = last + 1;
        void Segment(ulong at, uint entries)
        {
            if (entries == 0) return;
            var data = memory.Read(at, checked((int)entries * 16));
            for (var i = 0; i < entries; i++)
            {
                var handle = BitConverter.ToUInt32(data, i * 16);
                if ((handle & 0xF0800000) != 0xC0800000 || (handle & 0xFFFF) == 0xFFFF) continue;
                var pointer = BitConverter.ToUInt64(memory.Read(manager + 8 + (handle & 0xFFFF) * 8, 8));
                if (pointer < 0x10000 || pointer % 8 != 0) continue;
                var gem = memory.Read(pointer, 0x38);
                if (BitConverter.ToUInt64(gem) != session.ImageBase + 0x5336180 || BitConverter.ToUInt32(gem, 8) != handle || BitConverter.ToInt32(gem, 0x18) != 1) continue;
                var source = BitConverter.ToUInt32(gem, 0xC);
                if ((source & 0xF0000000) != 0x80000000) continue;
                var spec = new GemSpec((int)(source & 0x0FFFFFFF), BitConverter.ToInt32(gem, 0x1C), Enumerable.Range(0, 6).Select(s => BitConverter.ToInt32(gem, 0x20 + s * 4)).ToArray());
                result.Add(new(handle, spec));
            }
        }
        var firstCount = Math.Min(first, count);
        if (firstCount != 0) Segment(memory.Pointer(inventory + 0x58), firstCount);
        if (count > firstCount) Segment(memory.Pointer(inventory + 0x48), count - firstCount);
        if (session.ReadCharacter().Address != character.Address || memory.Int32(inventory + 0x24) != unchecked((int)first) || memory.Int32(inventory + 0x88) != unchecked((int)last))
            throw new IOException("Inventory changed during gem refresh. Refresh again after closing in-game menus.");
        return result.DistinctBy(x => x.Handle).ToArray();
    }
}

public sealed partial class GameActions
{
    public static readonly (ulong Offset, byte[] Bytes)[] GemFingerprints =
    [
        (0x1A87E20, Convert.FromHexString("554889E5415741564154534883EC30")),
        (0x1A87590, Convert.FromHexString("554889E54157415641554154534883EC")),
        (0x1A8E610, Convert.FromHexString("89771CC3")),
        (0x1A8E630, Convert.FromHexString("89F089548720C3")),
        (0x1F1DE91, Convert.FromHexString("83B8400C0000000F84B0000000488B80480C0000")),
        (0x1A8E3CF, Convert.FromHexString("488D05AA7D8A03488903")),
        (0x1A87ECC, Convert.FromHexString("498B560831C94885D274180FB68A8C000000"))
    ];
    private ulong gemHelper;
    public async Task<uint> SpawnGemAsync(GemSpec spec)
    {
        foreach (var f in GemFingerprints)
            if (!memory.Read(session.ImageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes)) throw new IOException("Native blood-gem instructions differ from the supported build. No gem was created.");
        spec.Validate();
        var gems = new BloodGems(memory, session);
        var dispatcher = Ready(GameActionKind.Gem);
        var character = session.ReadCharacter().Address;
        var before = gems.ReadInventory().Select(x => x.Handle).ToHashSet();
        var handles = memory.Pointer(session.ImageBase + 0x553E990);
        var manager = memory.Pointer(session.ImageBase + 0x553D6E0);
        gemHelper = gemHelper == 0 ? memory.AllocateCode(BuildGemHelper(session.ImageBase)) : gemHelper;
        await dispatcher.ExecuteAsync([new(gemHelper, dispatcher.Payload, handles, manager)], preparePayload: () =>
        {
            if (session.ReadCharacter().Address != character || memory.Pointer(session.ImageBase + 0x553E990) != handles || memory.Pointer(session.ImageBase + 0x553D6E0) != manager)
                throw new IOException("Character or item manager changed before gem creation. Refresh gems and retry after loading.");
            return gems.Prepare(spec);
        });
        var handle = unchecked((uint)memory.Int32(dispatcher.Payload + 160));
        if (memory.Int32(dispatcher.Payload + 164) != 1 || handle == 0) throw new IOException("The game could not allocate this gem. No automatic retry was made.");
        var awarded = gems.ReadInventory().FirstOrDefault(x => x.Handle == handle && !before.Contains(x.Handle));
        if (awarded == null || awarded.Spec.Source != spec.Source || awarded.Spec.Shape != spec.Shape || !awarded.Spec.Effects.SequenceEqual(spec.Effects))
            throw new IOException("Gem request ran, but the configured gem could not be confirmed in carried inventory. Check inventory/storage and capacity before retrying; the request was not repeated.");
        return handle;
    }
    internal static byte[] BuildGemHelper(ulong image)
    {
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
        void B(string hex) => w.Write(Convert.FromHexString(hex));
        void Call(ulong offset) { B("48B8"); w.Write(image + offset); B("FFD0"); }
        // SysV args: payload, handle manager, item manager. Native constructors and
        // award retain/release their references; no fabricated inventory handles.
        B("53415441554889FB4989F44989D5488D7B04488D5320"); Call(0x1A87E20);
        B("8B430485C00F84"); var failed = stream.Position; w.Write(0);
        B("8983A00000004C89EF4889DE"); Call(0x17D89F0);
        B("488D7B04"); Call(0x1A8BC00);
        B("C783A400000001000000"); var done = stream.Position;
        B("415D415C5BC3"); var end = stream.Position; stream.Position = failed; w.Write((int)(done - failed - 4)); stream.Position = end;
        return stream.ToArray();
    }
}
