using System.IO;

namespace BBTools.Memory;

// Two narrow runtime hooks. Never edits attributes, saves or shared loot rows.
public sealed class LootOverrides(ProcessMemory memory, PlayerSession session)
{
    public const ulong DiscoveryOffset = 0x158191B, DropOffset = 0x17CF52C;
    public static readonly byte[] DiscoveryOriginal = Convert.FromHexString("C5F258C0C5F057C9C5FA5FC1C30F1F840000000000");
    public static readonly byte[] DropOriginal = Convert.FromHexString("8B9C2498000000031848FF8424B0000000");
    public static readonly (ulong Offset, byte[] Bytes)[] Fingerprints =
    [
        (0x1581830, Convert.FromHexString("C5F057C94885FF7472F6472170746C")),
        (0x18C54FE, Convert.FromHexString("E8ED95F0FF448B45AC")),
        (0x17CF475, Convert.FromHexString("468854F715")),
        (0x17CF6A7, Convert.FromHexString("42807CFA1500")),
        (0x17CF567, Convert.FromHexString("85DB0F8E91F6FFFF")),
    ];
    private ulong controls, discoveryCode, dropCode;
    private readonly Dictionary<ulong, byte[]> installed = [];
    public void Apply(float bonusPoints, bool guaranteedDrops)
    {
        if (!float.IsFinite(bonusPoints) || bonusPoints is < 0 or > 1000)
            throw new ArgumentException("Discovery bonus must be between 0 and 1000.");
        session.ValidateImage(); session.ReadCharacter();
        foreach (var f in Fingerprints)
            if (!memory.Read(session.ImageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes))
                throw new IOException("Loot instructions differ from the supported 1.09 build.");
        if (bonusPoints == 0 && !guaranteedDrops) { Restore(); return; }
        var desired = new byte[8]; BitConverter.GetBytes(bonusPoints / 100f).CopyTo(desired, 0);
        BitConverter.GetBytes(guaranteedDrops ? 1 : 0).CopyTo(desired, 4);
        if (controls != 0 && installed.Count == (bonusPoints > 0 ? 1 : 0) + (guaranteedDrops ? 1 : 0) &&
            installed.All(p => memory.Read(session.ImageBase + p.Key, p.Value.Length).SequenceEqual(p.Value)) && memory.Read(controls, 8).SequenceEqual(desired)) return;
        memory.Suspended(() =>
        {
            session.ReadCharacter();
            foreach (var (at, bytes) in installed)
                if (!memory.Read(session.ImageBase + at, bytes.Length).SequenceEqual(bytes))
                { Pause(); throw new IOException("Another tool changed a loot hook. Its instructions were retained."); }
            if (controls == 0)
            {
                controls = memory.AllocateData(16);
                discoveryCode = memory.AllocateCode(BuildDiscovery(session.ImageBase, controls));
                dropCode = memory.AllocateCode(BuildDrops(session.ImageBase, controls));
            }
            if (bonusPoints > 0) Install(DiscoveryOffset, DiscoveryOriginal, discoveryCode);
            else Remove(DiscoveryOffset, DiscoveryOriginal);
            if (guaranteedDrops) Install(DropOffset, DropOriginal, dropCode);
            else Remove(DropOffset, DropOriginal);
            if (!memory.Read(controls, 8).SequenceEqual(desired)) memory.Write(controls, desired);
        });
    }
    private void Install(ulong offset, byte[] original, ulong code)
    {
        if (installed.ContainsKey(offset)) return;
        var patch = Convert.FromHexString("FF2500000000").Concat(BitConverter.GetBytes(code))
            .Concat(Enumerable.Repeat((byte)0x90, original.Length - 14)).ToArray();
        memory.ReplaceLootCode(session.ImageBase, offset, original, patch); installed[offset] = patch;
    }
    private void Remove(ulong offset, byte[] original)
    {
        if (!installed.TryGetValue(offset, out var patch)) return;
        memory.ReplaceLootCode(session.ImageBase, offset, patch, original); installed.Remove(offset);
    }
    public void Pause() { if (controls != 0 && !memory.Read(controls, 8).SequenceEqual(new byte[8])) memory.Write(controls, new byte[8]); }
    public void Restore()
    {
        if (controls == 0 || installed.Count == 0) return;
        memory.Suspended(() =>
        {
            Pause(); var foreign = false;
            foreach (var (offset, patch) in installed.ToArray())
            {
                if (!memory.Read(session.ImageBase + offset, patch.Length).SequenceEqual(patch)) { foreign = true; continue; }
                Remove(offset, offset == DiscoveryOffset ? DiscoveryOriginal : DropOriginal);
            }
            if (foreign) throw new IOException("A loot hook changed externally. Its instructions were preserved; this tool's bonus was disabled.");
        });
        // Native threads may retain code addresses: keep private pages until process exit.
    }
    public static byte[] BuildDiscovery(ulong image, ulong controls)
    {
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
        var exits = new List<long>();
        void B(string hex) => w.Write(Convert.FromHexString(hex));
        void Exit(string op) { B(op); exits.Add(stream.Position); w.Write(0); }
        // Original arithmetic: Arcane curve + equipped effects, clamped >= 0.
        w.Write(DiscoveryOriginal[..12]); B("9C5048B8"); w.Write(image + 0x553B130);
        B("488B004885C0"); Exit("0F84"); B("488B40084885C0"); Exit("0F84");
        // Only the active character record's discovery-effect container.
        B("4839B870060000"); Exit("0F85");
        B("48B8"); w.Write(controls); B("C5FA1008C5FA58C1");
        var end = stream.Position; B("589DC3");
        foreach (var at in exits) { stream.Position = at; w.Write((int)(end - at - 4)); }
        return stream.ToArray();
    }
    public static byte[] BuildDrops(ulong image, ulong controls)
    {
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
        var exits = new List<long>();
        void B(string hex) => w.Write(Convert.FromHexString(hex));
        void Exit(string op) { B(op); exits.Add(stream.Position); w.Write(0); }
        B("9C5248BA"); w.Write(controls); B("837A0400"); Exit("0F84");
        B("48BA"); w.Write(image + 0x18C5503); B("48395508"); Exit("0F85");
        // RAX is this already eligible slot's weight, after level-dependent
        // item resolution. Drop only blank/invalid outcomes, never boost rare
        // weights or bypass event flags. The normal weighted draw stays intact.
        B("8B50F80B50FC"); var clearBranches = new List<long>();
        B("0F88"); clearBranches.Add(stream.Position); w.Write(0);
        B("80780D00"); Exit("0F85");
        var clear = stream.Position; B("C70000000000");
        var end = stream.Position; B("5A9D"); w.Write(DropOriginal);
        B("FF2500000000"); w.Write(image + DropOffset + (ulong)DropOriginal.Length);
        foreach (var at in exits) { stream.Position = at; w.Write((int)(end - at - 4)); }
        foreach (var at in clearBranches) { stream.Position = at; w.Write((int)(clear - at - 4)); }
        return stream.ToArray();
    }
}
