using System.IO;

namespace BBTools.Memory;

public sealed record MenuChoice(string Name, ulong Factory)
{
    public string Label => Name;
    public override string ToString() => Label;
}

public sealed partial class GameActions
{
    private ulong menuHelper;
    public static MenuChoice[] Menus =>
    [
        new("Level up", 0x1BB7F60), new("Fortify weapons", 0x1BB9D00),
        new("Repair weapons", 0x1BB9780), new("Storage", 0x1BBA4F0),
        new("Blood gem workshop", 0x1BBAA90), new("Caryll rune workshop", 0x1BBB050),
    ];
    public static readonly (ulong Offset, byte[] Bytes)[] MenuFingerprints =
    [
        (0x1769360, Convert.FromHexString("488B3D0995DF034885FF")),
        (0x1BB27A0, Convert.FromHexString("554889E54157415641554154534883EC38")),
        (0x1BB7F60, Convert.FromHexString("554889E541574156534883EC484889FB")),
        (0x1BB9D00, Convert.FromHexString("554889E54157415641554154534881EC48020000")),
        (0x1BB9780, Convert.FromHexString("554889E54157415641554154534881EC08030000")),
        (0x1BBA4F0, Convert.FromHexString("554889E541574156534883EC484889FB")),
        (0x1BBAA90, Convert.FromHexString("554889E54157415641554154534881EC98030000")),
        (0x1BBB050, Convert.FromHexString("554889E54157415641554154534881ECB8010000")),
        (0x1BB1814, Convert.FromHexString("488BB520FCFFFFE840670000")),
        (0x1BB1989, Convert.FromHexString("488BB520FCFFFFE8EB7D0000")),
        (0x1BB1A02, Convert.FromHexString("488BB520FCFFFFE8F2820000")),
    ];
    public async Task OpenMenuAsync(MenuChoice menu)
    {
        if (!Menus.Contains(menu)) throw new ArgumentException("Choose a supported workshop menu.");
        await OpenMenuCoreAsync(menu);
    }
    private async Task OpenMenuCoreAsync(MenuChoice menu)
    {
        var dispatcher = Ready();
        foreach (var f in MenuFingerprints)
            if (!memory.Read(session.ImageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes))
                throw new IOException("Menu instructions differ from the supported 1.09 build.");
        var manager = memory.Pointer(session.ImageBase + 0x5562870);
        if (BitConverter.ToUInt64(memory.Read(manager + 0xA88, 8)) != 0)
            throw new IOException("Close the current game menu before opening another.");
        menuHelper = menuHelper == 0 ? memory.AllocateCode(BuildMenuHelper(session.ImageBase)) : menuHelper;
        var payload = new byte[96];
        BitConverter.GetBytes(manager).CopyTo(payload, 0);
        BitConverter.GetBytes(session.ImageBase + menu.Factory).CopyTo(payload, 8);
        await dispatcher.ExecuteAsync([new(menuHelper, dispatcher.Payload)], payload);
        if (memory.Int32(dispatcher.Payload + 80) != 1)
            throw new IOException("The menu could not be opened. Close other menus and try again while idle.");
    }
    public static byte[] BuildMenuHelper(ulong imageBase)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        var labels = new Dictionary<string, long>(); var branches = new List<(long At, string Label)>();
        void B(string hex) => writer.Write(Convert.FromHexString(hex));
        void L(string name) => labels[name] = stream.Position;
        void J(string opcode, string label) { B(opcode); branches.Add((stream.Position, label)); writer.Write(0); }
        // SysV calls use a native-created menu step and the engine's attach routine.
        // No quest flags, upgrade costs or material requirements are changed.
        B("554889E55341544889FB4C8B23");
        B("48B8"); writer.Write(imageBase + 0x5562870); B("4C3920"); J("0F85", "end");
        B("4983BC24880A000000"); J("0F85", "end");
        B("488D7B10498D742448FF530848837B1000"); J("0F84", "end");
        B("488D7B184C89E6488D5310488D4B2048B8"); writer.Write(imageBase + 0x1BB27A0); B("FFD0");
        // Drop only our temporary shared references. MenuMan owns the active step
        // and its ordinary close callback, including when BB Tools disconnects.
        foreach (var offset in new byte[] { 16, 24 })
        {
            B("488B7B"); writer.Write(offset); B("4885FF"); J("0F84", $"released{offset}");
            B("F0836F0801"); J("0F85", $"released{offset}"); B("488B07FF10"); L($"released{offset}");
            B("48C743"); writer.Write(offset); writer.Write(0);
        }
        B("4983BC24880A000000"); J("0F84", "end"); B("C7435001000000");
        L("end"); B("415C5B5DC3");
        var length = stream.Position;
        foreach (var fixup in branches) { stream.Position = fixup.At; writer.Write((int)(labels[fixup.Label] - fixup.At - 4)); }
        stream.Position = length; return stream.ToArray();
    }
}
