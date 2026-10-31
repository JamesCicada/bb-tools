using System.IO;

namespace BBTools.Memory;

public sealed record ChaliceAltar(int Bank, uint MapUid, int Status)
{
    public string Label => $"Altar {Bank} · " + (MapUid == 0 ? "no loaded dungeon" : $"dungeon {MapUid:X8}") +
        (Status == 0 ? " · suspended" : Status is 4 or 5 ? " · ready" : " · unavailable");
    public override string ToString() => Label;
}

public sealed record ChaliceLamp(string Name, int Slot, int UnlockFlag)
{
    public int Entity => 2902950 + Slot;
    public string Label => Name;
    public override string ToString() => Label;
}

public sealed partial class GameActions
{
    private ulong chaliceWarpHelper;
    public static ChaliceLamp[] ChaliceLamps =>
    [
        new("Chamber of the Seal", 0, 0), new("Layer One", 1, 12907830),
        new("Layer One: Heart", 2, 12907850), new("Layer Two", 3, 12907870),
        new("Layer Two: Heart", 4, 12907890), new("Layer Three", 5, 12907910),
        new("Layer Three: Heart", 6, 12907930), new("Layer Four", 7, 12907950),
        new("Layer Four: Heart", 8, 12907970),
    ];
    public static readonly (ulong Offset, byte[] Bytes)[] ChaliceFingerprints =
    [
        (0x13B89B0, Convert.FromHexString("554889E5415741564154534881EC90030000")),
        (0x1AA40E0, Convert.FromHexString("554889E54156534883EC204889FB4C8B35EBFA93")),
        (0x13CFD80, Convert.FromHexString("5541574156534189D0448B4F1C31D289F0")),
        (0x13D0060, Convert.FromHexString("5541574156534189C84189D1448B571C31D289F0")),
    ];
    private (ulong Owner, ulong Gate, ulong Helper) ChaliceGate(int bank)
    {
        if (bank is < 1 or > 6) throw new ArgumentException("Choose one of the six ordinary chalice altars.");
        session.ValidateImage();
        var owner = memory.Pointer(session.ImageBase + 0x553E9E8);
        if (memory.Int32(owner) != 7) throw new IOException("The character's chalice altars are not loaded.");
        var gate = memory.Pointer(memory.Pointer(owner + 8) + (ulong)bank * 8);
        if (memory.Int32(gate + 0x100) != bank || memory.Pointer(gate) != session.ImageBase + 0x5336570)
            throw new IOException("The chalice altar layout differs from the supported profile.");
        return (owner, gate, gate + 0x108);
    }
    public ChaliceAltar[] ReadChaliceAltars()
    {
        var result = new List<ChaliceAltar>();
        for (var bank = 1; bank <= 6; bank++)
        {
            var (_, _, helper) = ChaliceGate(bank);
            result.Add(new(bank, unchecked((uint)memory.Int32(helper + 0xE0)), memory.Int32(helper + 0x25C)));
        }
        return result.ToArray();
    }
    public async Task WarpChaliceAsync(int bank, ChaliceLamp lamp)
    {
        if (!ChaliceLamps.Contains(lamp)) throw new ArgumentException("Choose a supported chalice lamp.");
        if ((unchecked((uint)memory.Int32(session.ReadPlayer().Actor + 0x3F8)) >> 24) != 21)
            throw new IOException("Chalice travel starts from Hunter's Dream. Travel there with the lamp controls, then choose your altar and layer.");
        var dispatcher = Ready(GameActionKind.Warp);
        foreach (var f in ChaliceFingerprints)
        {
            var actual = memory.Read(session.ImageBase + f.Offset, f.Bytes.Length);
            if (!actual.SequenceEqual(f.Bytes))
                throw new IOException($"Chalice instructions differ at image+0x{f.Offset:X}: expected {Convert.ToHexString(f.Bytes)}, actual {Convert.ToHexString(actual)}. " +
                    (f.Offset == 0x13CFD80 && actual[0] == 0xE9 ? "The runtime redirects the chalice flag reader; this replacement is not yet supported. " : "") +
                    "No chalice travel request was submitted. Include this session log in compatibility reports.");
        }
        var manager = memory.Pointer(session.ImageBase + 0x5562870);
        if (BitConverter.ToUInt64(memory.Read(manager + 0xA88, 8)) != 0)
            throw new IOException("Close the current game menu before choosing a chalice altar.");
        var (owner, gate, helper) = ChaliceGate(bank);
        if (memory.Int32(helper + 0x260) != 0 || memory.Int32(helper + 0x264) != 0)
            throw new IOException("This altar is processing another ritual request. Wait for it to finish.");
        var flags = memory.Pointer(session.ImageBase + 0x553B100);
        // Native selection remaps the selected bank's dungeon and progress flags.
        // A fixed m29 lamp ID alone cannot identify a character's generated dungeon.
        await dispatcher.ExecuteAsync([new(session.ImageBase + 0x13B89B0, flags, (ulong)bank)]);
        if (memory.Int32(helper + 0x25C) == 0)
            await dispatcher.ExecuteAsync([new(session.ImageBase + 0x1AA40E0, helper)]);
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (memory.Int32(helper + 0x260) != 0 || memory.Int32(helper + 0x264) != 0)
        {
            if (DateTime.UtcNow >= deadline) throw new IOException("Altar resume is still pending. Check the game before retrying.");
            await Task.Delay(100);
            if (ChaliceGate(bank) != (owner, gate, helper)) throw new IOException("The altar changed during the request.");
        }
        if (memory.Int32(helper + 0x25C) is not (4 or 5) || memory.Int32(helper + 0xE0) == 0)
            throw new IOException("This altar has no completed dungeon ready for travel. Complete its ritual in the game first.");
        if (ChaliceGate(bank) != (owner, gate, helper)) throw new IOException("The altar changed during the request.");
        var checks = new List<NativeCall>
        {
            new(session.ImageBase + 0x13CFD80, flags, 0x59A5380UL + (ulong)bank * 100000, 32),
            new(session.ImageBase + 0x13CFD80, flags, 0x233C, 7),
        };
        if (lamp.UnlockFlag != 0) checks.Add(new(session.ImageBase + 0x13CFD80, flags, (ulong)lamp.UnlockFlag, 1));
        await dispatcher.ExecuteAsync(checks.ToArray());
        if ((uint)dispatcher.Result(0) != unchecked((uint)memory.Int32(helper + 0xE0)) ||
            dispatcher.Result(1) != (1UL << (6 - bank)))
            throw new IOException("The selected altar's generated-dungeon context does not match. Travel was not requested.");
        if (lamp.UnlockFlag != 0 && dispatcher.Result(2) == 0)
            throw new IOException("This lamp is not unlocked in the selected dungeon. Choose an unlocked layer or the Chamber of the Seal.");
        var payload = new byte[64];
        ulong[] fields = [flags, unchecked((uint)memory.Int32(helper + 0xE0)), 1UL << (6 - bank), (ulong)lamp.Entity, (ulong)lamp.Slot];
        for (var i = 0; i < fields.Length; i++) BitConverter.GetBytes(fields[i]).CopyTo(payload, i * 8);
        BitConverter.GetBytes(session.ReadPlayer().Actor).CopyTo(payload, 48);
        BitConverter.GetBytes(0x59A5380UL + (ulong)bank * 100000).CopyTo(payload, 56);
        chaliceWarpHelper = chaliceWarpHelper == 0 ? memory.AllocateCode(BuildChaliceWarpHelper(session.ImageBase)) : chaliceWarpHelper;
        await dispatcher.ExecuteAsync([new(chaliceWarpHelper, dispatcher.Payload)], payload);
        if (memory.Int32(dispatcher.Payload + 40) != 1) throw new IOException("The selected dungeon changed before travel. No warp was requested.");
    }
    public static byte[] BuildChaliceWarpHelper(ulong imageBase)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        var exits = new List<long>();
        void B(string hex) => writer.Write(Convert.FromHexString(hex));
        void Fn(ulong rva) { B("48B8"); writer.Write(imageBase + rva); B("FFD0"); }
        void RejectChanged() { B("0F85"); exits.Add(stream.Position); writer.Write(0); }
        B("554889E55341544889FB4C8B23488B43308B80F8030000C1E81883F815"); RejectChanged();
        B("48B8"); writer.Write(imageBase + 0x553B100); B("4C3920"); RejectChanged();
        B("4C89E78B7338BA20000000"); Fn(0x13CFD80); B("3B4308"); RejectChanged();
        B("4C89E7BE3C230000BA07000000"); Fn(0x13CFD80); B("483B4310"); RejectChanged();
        B("8B7B18"); Fn(0x156D110);
        B("BF01000000"); Fn(0x156D0E0);
        B("31FF8B7318"); Fn(0x132E050);
        // Mirror Dream event 12107200's destination bit, after its warp request.
        B("4C89E78B732081C629230000BA01000000B901000000"); Fn(0x13D0060);
        B("C7432801000000"); var end = stream.Position; B("415C5B5DC3");
        var length = stream.Position;
        foreach (var fixup in exits) { stream.Position = fixup; writer.Write((int)(end - fixup - 4)); }
        stream.Position = length; return stream.ToArray();
    }
}
