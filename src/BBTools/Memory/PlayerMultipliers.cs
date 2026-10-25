using System.IO;
using System.Security.Cryptography;

namespace BBTools.Memory;

public sealed partial class GameActions
{
    private const int MultiplierEffect = 9999;
    internal const float MultiplierLeaseSeconds = 5;
    private ulong effectActor, effectContainer, effectNode, effectRow, originalEffectRow, effectHelper;
    private long lastMultiplierRefresh;
    private byte[] ReadNeutralEffectRow(ulong address)
    {
        var row = memory.Read(address, 0x210);
        // A modded "neutral" row could contain unrelated damage/HP/status effects.
        // Refuse it rather than activating unrelated effects.
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
        // A renewable lease expires through the engine even if the trainer is
        // killed or loses the actor during loading. Never leave an infinite buff.
        BitConverter.GetBytes(MultiplierLeaseSeconds).CopyTo(row, 8);
        for (var i = 0; i < 4; i++) BitConverter.GetBytes(damage).CopyTo(row, 0x38 + 4 * i);
        BitConverter.GetBytes(echoes).CopyTo(row, 0xE0);
        return row;
    }
    internal static bool IsTrainerMultiplierRow(byte[] row, byte[] neutral)
    {
        if (row.Length != 0x210 || neutral.Length != 0x210) return false;
        var damage = BitConverter.ToSingle(row, 0x38);
        var echoes = BitConverter.ToSingle(row, 0xE0);
        if (!float.IsFinite(damage) || !float.IsFinite(echoes) || damage is < 0.1f or > 100 || echoes is < 0.1f or > 100)
            return false;
        var duration = BitConverter.ToSingle(row, 8);
        if (duration != -1 && duration != MultiplierLeaseSeconds) return false;
        var expected = MultiplierRow(neutral, damage, echoes);
        // Recognize the infinite private clones made by versions <= 0.6.1 too.
        BitConverter.GetBytes(duration).CopyTo(expected, 8);
        return row.SequenceEqual(expected);
    }
    // Caller validates the table row's hash first. Inspect every collision before
    // writing anything; a vanilla/foreign 9999 must never be silently overwritten.
    internal int RecoverMultiplierEffects(ulong actor, ulong original, byte[] neutral)
    {
        var container = memory.Pointer(actor + 0x1C8);
        var nodes = EffectNodes(actor, container).Where(n => memory.Int32(n + 0x40) == MultiplierEffect && memory.Read(n + 0x2D, 1)[0] == 0).ToArray();
        foreach (var node in nodes)
        {
            var row = memory.Pointer(node + 0x48);
            if (row == original || !IsTrainerMultiplierRow(memory.Read(row, 0x210), neutral))
                throw new IOException($"Effect 9999 is in use by the game or another mod (node 0x{node:X}, row 0x{row:X}, Echoes {BitConverter.ToSingle(memory.Read(row + 0xE0, 4))}x). Multipliers were not changed.");
        }
        foreach (var node in nodes)
        {
            memory.Write(node + 0x48, BitConverter.GetBytes(original));
            memory.Write(node, new byte[8]);
            memory.Write(node + 0x2D, [1]);
        }
        return nodes.Length;
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
            memory.Int32(effectNode + 0x40) == MultiplierEffect && memory.Pointer(effectNode + 0x48) == effectRow &&
            memory.Read(effectNode + 0x2D, 1)[0] == 0 && (memory.Int32(effectNode + 0x1C) & unchecked((int)0x800C0003)) == 0;
    }
    public bool MaintainMultipliers()
    {
        if (!MultipliersAttached()) return false;
        var attached = true;
        if (Environment.TickCount64 - lastMultiplierRefresh >= 1000)
            memory.Suspended(() =>
            {
                attached = MultipliersAttached();
                if (!attached) return;
                memory.Write(effectNode, BitConverter.GetBytes(MultiplierLeaseSeconds));
                lastMultiplierRefresh = Environment.TickCount64;
            });
        return attached;
    }
    public async Task SetMultipliersAsync(float damage, float echoes)
    {
        // Validate numeric input before performing any native request.
        if (!float.IsFinite(damage) || !float.IsFinite(echoes) || damage is < 0.1f or > 100 || echoes is < 0.1f or > 100)
            throw new ArgumentException("Multipliers must be between 0.1 and 100.");
        if (damage == 1 && echoes == 1)
        {
            // A default startup preset needs no native request when there is
            // nothing to recover. Still inspect unowned leftovers after reset.
            ResetMultipliers();
            var current = session.ReadPlayer().Actor;
            var holder = memory.Pointer(current + 0x1C8);
            if (!EffectNodes(current, holder).Any(n => memory.Int32(n + 0x40) == MultiplierEffect && memory.Read(n + 0x2D, 1)[0] == 0))
                return;
        }
        var dispatcher = Ready();
        foreach (var f in MultiplierFingerprints)
            if (!memory.Read(session.ImageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes))
                throw new IOException("Player effect instructions differ from the supported 1.09 build.");
        if (MultipliersAttached() && (damage != 1 || echoes != 1))
        {
            var row = MultiplierRow(ReadNeutralEffectRow(originalEffectRow), damage, echoes);
            memory.Suspended(() => { if (!MultipliersAttached()) throw new IOException("Player effects changed."); memory.Write(effectRow, row); memory.Write(effectNode, BitConverter.GetBytes(MultiplierLeaseSeconds)); });
            App.Log?.Write("MULTIPLIERS", $"Updated existing trainer effect: damage {damage}x, Echoes {echoes}x; actor 0x{effectActor:X}.");
            return;
        }
        var actor = session.ReadPlayer().Actor;
        var container = memory.Pointer(actor + 0x1C8);
        await dispatcher.ExecuteAsync([new(session.ImageBase + 0x1F28AA0, dispatcher.Payload, MultiplierEffect)], new byte[16]);
        var original = memory.Pointer(dispatcher.Payload + 8);
        var neutral = ReadNeutralEffectRow(original);
        var recovered = 0;
        memory.Suspended(() =>
        {
            if (session.ReadPlayer().Actor != actor) throw new IOException("Player changed before multiplier recovery.");
            recovered = RecoverMultiplierEffects(actor, original, neutral);
        });
        App.Log?.Write("MULTIPLIERS", $"Requested damage {damage}x, Echoes {echoes}x; removed {recovered} previous trainer effect(s); actor 0x{actor:X}.");
        effectNode = 0;
        if (damage == 1 && echoes == 1) return;
        var clone = memory.AllocateData(0x210);
        memory.Write(clone, MultiplierRow(neutral, damage, echoes));
        effectHelper = effectHelper == 0 ? memory.AllocateCode(BuildEffectHelper(session.ImageBase)) : effectHelper;
        var payload = new byte[64];
        ulong[] fields = [actor, container, clone, original];
        for (var i = 0; i < fields.Length; i++) BitConverter.GetBytes(fields[i]).CopyTo(payload, i * 8);
        await dispatcher.ExecuteAsync([new(effectHelper, dispatcher.Payload)], payload);
        var status = memory.Int32(dispatcher.Payload + 48);
        if (status != 1)
        {
            App.Log?.Write("MULTIPLIERS", $"Native insertion failed at stage {status}; actor 0x{actor:X}, container 0x{container:X}.");
            throw new IOException(status switch
            {
                -1 or -2 => "The player or effect owner changed during loading. Wait for the character to settle and apply again.",
                -3 => "Effect 9999 is already active in the game or another mod. No existing effect was overwritten.",
                -4 => "The player effect list exceeded its verified limit. Multipliers were not applied.",
                -5 => "The game rejected multiplier effect insertion. No effect was replaced.",
                -6 => "The native multiplier effect result was not found in the current player list.",
                -7 => "The native multiplier effect row differs from the verified neutral row. No effect was replaced.",
                _ => $"Multiplier insertion returned an unexpected result ({status}). Check the session log."
            });
        }
        effectActor = actor; effectContainer = container; effectRow = clone; originalEffectRow = original;
        effectNode = memory.Pointer(dispatcher.Payload + 56);
        lastMultiplierRefresh = Environment.TickCount64;
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
        void Status(int value) { B("C74330"); writer.Write(value); }
        void Fn(ulong rva) { B("48B8"); writer.Write(imageBase + rva); B("FFD0"); }
        // SysV helper: RBX=payload, R12=container. Six pushes plus 24 local bytes
        // align RSP before calls and reserve the seventh integer argument.
        B("554889E55341544155415641574883EC184889FB4C8B6308"); Status(-1);
        B("488B034C39A0C8010000"); J("0F85", "end");
        Status(-2);
        B("4939442418"); J("0F85", "end");
        // Check again on the game thread before creating anything.
        B("498B442408B900080000"); L("scan"); B("4885C0"); J("0F84", "insert");
        B("8178400F270000"); J("0F85", "scanNext"); B("80782D00"); J("0F84", "collision"); L("scanNext"); B("488B4058FFC9"); J("0F85", "scan"); Status(-4); J("E9", "end");
        L("insert"); B("4C89E7BE0F27000031D231C94531C04531C948C7042400000000");
        B("B80000803F660F6EC00F28C80F28D00F28D80F28E0"); Fn(0x17E3130);
        // The verified neutral row has duration zero, so native insertion marks
        // its new node expired immediately. Identify the exact returned index;
        // never skip that node or revive an older expired node with the same ID.
        Status(-5); B("85C0"); J("0F88", "end"); B("3D00080000"); J("0F83", "end");
        B("89C1498B442408"); Status(-6);
        L("find"); B("4885C0"); J("0F84", "end"); B("85C9"); J("0F84", "attach");
        B("488B4058FFC9"); J("E9", "find");
        L("attach"); B("8178400F270000"); J("0F85", "end");
        Status(-7); B("488B531848395048"); J("0F85", "end");
        B("488B53104889504848BA"); writer.Write(MultiplierLeaseSeconds); writer.Write(MultiplierLeaseSeconds);
        B("488910C6402D00488943384889C7660FEFC0"); Fn(0x17EA980);
        Status(1); J("E9", "end"); L("collision"); Status(-3);
        L("end"); B("4883C418415F415E415D415C5B5DC3");
        var length = stream.Position;
        foreach (var fixup in branches) { stream.Position = fixup.At; writer.Write((int)(labels[fixup.Label] - fixup.At - 4)); }
        stream.Position = length; return stream.ToArray();
    }
}
