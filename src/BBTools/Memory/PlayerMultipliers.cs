using System.IO;
using System.Security.Cryptography;

namespace BBTools.Memory;

public sealed partial class GameActions
{
    private const int MultiplierEffect = 9999;
    private ulong effectActor, effectContainer, effectNode, effectRow, originalEffectRow, effectHelper;
    private byte[] ReadNeutralEffectRow(ulong address)
    {
        var row = memory.Read(address, 0x210);
        // A modded "neutral" row could contain unrelated damage/HP/status effects.
        // Refuse it rather than activating those effects with an infinite duration.
        if (!Convert.ToHexString(SHA256.HashData(row)).Equals("F1FC024B10AA425CE40911B1B51590959B2653F6FCAA6731E7F56356B09F77EA", StringComparison.Ordinal))
            throw new IOException("SpEffect 9999 was modified by another game-data mod. Multipliers were not applied.");
        return row;
    }
    public static readonly (ulong Offset, byte[] Bytes)[] MultiplierFingerprints =
    [
        (0x1F28AA0, Convert.FromHexString("554889E5415741564154534189F44989")),
        (0x17E3130, Convert.FromHexString("554889E54157415641554154534883EC")),
        (0x17EA980, Convert.FromHexString("8B471CA9030000807406807F2C007420")),
    ];
    public static byte[] MultiplierRow(byte[] neutral, float damage, float echoes)
    {
        if (!float.IsFinite(damage) || !float.IsFinite(echoes) || damage is < 0.1f or > 100 || echoes is < 0.1f or > 100)
            throw new ArgumentException("Multipliers must be between 0.1 and 100.");
        if (neutral.Length != 0x210 || BitConverter.ToSingle(neutral, 0xE0) != 1 || neutral[0x141] != 0xFF ||
            Enumerable.Range(0, 4).Any(i => BitConverter.ToSingle(neutral, 0x38 + 4 * i) != 1))
            throw new IOException("The neutral effect row differs from the supported profile.");
        var row = neutral.ToArray();
        BitConverter.GetBytes(-1f).CopyTo(row, 8); // Infinite duration, no saved category.
        for (var i = 0; i < 4; i++) BitConverter.GetBytes(damage).CopyTo(row, 0x38 + 4 * i);
        BitConverter.GetBytes(echoes).CopyTo(row, 0xE0);
        return row;
    }
    private ulong[] EffectNodes(ulong actor, ulong container)
    {
        if (memory.Pointer(actor + 0x1C8) != container || memory.Pointer(container + 0x18) != actor)
            throw new IOException("Player effect ownership changed.");
        var nodes = new List<ulong>(); var seen = new HashSet<ulong>();
        var node = BitConverter.ToUInt64(memory.Read(container + 8, 8));
        while (node != 0)
        {
            if (nodes.Count >= 2048 || !seen.Add(node)) throw new IOException("Invalid player effect list.");
            nodes.Add(node); node = BitConverter.ToUInt64(memory.Read(node + 0x58, 8));
        }
        return nodes.ToArray();
    }
    public bool MultipliersAttached()
    {
        var actor = session.ReadPlayer().Actor;
        return effectNode != 0 && actor == effectActor && EffectNodes(actor, effectContainer).Contains(effectNode) &&
            memory.Int32(effectNode + 0x40) == MultiplierEffect && memory.Pointer(effectNode + 0x48) == effectRow;
    }
    public async Task SetMultipliersAsync(float damage, float echoes)
    {
        // Validate numeric input before performing any native request.
        if (!float.IsFinite(damage) || !float.IsFinite(echoes) || damage is < 0.1f or > 100 || echoes is < 0.1f or > 100)
            throw new ArgumentException("Multipliers must be between 0.1 and 100.");
        if (damage == 1 && echoes == 1) { ResetMultipliers(); return; }
        var dispatcher = Ready();
        foreach (var f in MultiplierFingerprints)
            if (!memory.Read(session.ImageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes))
                throw new IOException("Player effect instructions differ from the supported 1.09 build.");
        if (MultipliersAttached())
        {
            var row = MultiplierRow(ReadNeutralEffectRow(originalEffectRow), damage, echoes);
            memory.Suspended(() => { if (!MultipliersAttached()) throw new IOException("Player effects changed."); memory.Write(effectRow, row); });
            return;
        }
        effectNode = 0;
        var actor = session.ReadPlayer().Actor;
        var container = memory.Pointer(actor + 0x1C8);
        if (EffectNodes(actor, container).Any(node => memory.Int32(node + 0x40) == MultiplierEffect))
            throw new IOException("Effect 9999 is already in use by the game or another mod.");
        await dispatcher.ExecuteAsync([new(session.ImageBase + 0x1F28AA0, dispatcher.Payload, MultiplierEffect)], new byte[16]);
        var original = memory.Pointer(dispatcher.Payload + 8);
        var clone = memory.AllocateData(0x210);
        memory.Write(clone, MultiplierRow(ReadNeutralEffectRow(original), damage, echoes));
        effectHelper = effectHelper == 0 ? memory.AllocateCode(BuildEffectHelper(session.ImageBase)) : effectHelper;
        var payload = new byte[64];
        ulong[] fields = [actor, container, clone, original];
        for (var i = 0; i < fields.Length; i++) BitConverter.GetBytes(fields[i]).CopyTo(payload, i * 8);
        await dispatcher.ExecuteAsync([new(effectHelper, dispatcher.Payload)], payload);
        if (memory.Int32(dispatcher.Payload + 48) != 1) throw new IOException("The player changed or an effect collision prevented applying multipliers.");
        effectActor = actor; effectContainer = container; effectRow = clone; originalEffectRow = original;
        effectNode = memory.Pointer(dispatcher.Payload + 56);
    }
    public void ResetMultipliers()
    {
        if (effectNode == 0) return;
        memory.Suspended(() =>
        {
            // Only our current player's native-owned node may be restored. Never free
            // a game node from the trainer or touch an unloaded actor's allocation.
            if (MultipliersAttached())
            {
                memory.Write(effectNode + 0x48, BitConverter.GetBytes(originalEffectRow));
                memory.Write(effectNode, new byte[8]);
                memory.Write(effectNode + 0x2D, [1]); // Expire through the engine's normal update.
            }
            effectNode = 0;
        });
    }
    public static byte[] BuildEffectHelper(ulong imageBase)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        var labels = new Dictionary<string, long>(); var branches = new List<(long At, string Label)>();
        void B(string hex) => writer.Write(Convert.FromHexString(hex));
        void L(string name) => labels[name] = stream.Position;
        void J(string opcode, string label) { B(opcode); branches.Add((stream.Position, label)); writer.Write(0); }
        void Fn(ulong rva) { B("48B8"); writer.Write(imageBase + rva); B("FFD0"); }
        // SysV helper: RBX=payload, R12=container. Six pushes plus 24 local bytes
        // align RSP before calls and reserve the seventh integer argument.
        B("554889E55341544155415641574883EC184889FB4C8B6308C7433000000000");
        B("488B034C39A0C8010000"); J("0F85", "end");
        B("4939442418"); J("0F85", "end");
        // Check again on the game thread before creating anything.
        B("498B442408B900080000"); L("scan"); B("4885C0"); J("0F84", "insert");
        B("8178400F270000"); J("0F84", "end"); B("488B4058FFC9"); J("0F85", "scan"); J("E9", "end");
        L("insert"); B("4C89E7BE0F27000031D231C94531C04531C948C7042400000000");
        B("B80000803F660F6EC00F28C80F28D00F28D80F28E0"); Fn(0x17E3130);
        B("498B442408B900080000"); L("find"); B("4885C0"); J("0F84", "end");
        B("8178400F270000"); J("0F84", "attach"); B("488B4058FFC9"); J("0F85", "find"); J("E9", "end");
        L("attach"); B("488B531848395048"); J("0F85", "end");
        B("488B53104889504848BA000080BF000080BF488910C6402D00488943384889C7660FEFC0"); Fn(0x17EA980);
        B("C7433001000000"); L("end"); B("4883C418415F415E415D415C5B5DC3");
        var length = stream.Position;
        foreach (var fixup in branches) { stream.Position = fixup.At; writer.Write((int)(labels[fixup.Label] - fixup.At - 4)); }
        stream.Position = length; return stream.ToArray();
    }
}
