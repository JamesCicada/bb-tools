using System.IO;

namespace BBTools.Memory;

public enum PracticeFlag { GodMode, OneShot, Stealth, Silence }

public sealed partial class PlayerSession
{
    private readonly Dictionary<PracticeFlag, (ulong Owner, byte Original)> ownedFlags = [];
    public static readonly (ulong Offset, byte[] Bytes)[] FeatureFingerprints =
    [
        (0x1A2FD81, Convert.FromHexString("F6800002000002")),
        (0x1A57873, Convert.FromHexString("F6830002000001")),
        (0x19211E8, Convert.FromHexString("488D159AD6C103")),
        (0x19214BE, Convert.FromHexString("488D15C9D3C103")),
        (0x1921500, Convert.FromHexString("488D1588D3C103"))
    ];
    public void SetPracticeFlag(PracticeFlag flag, bool enabled)
    {
        ValidateImage();
        foreach (var f in FeatureFingerprints)
            if (!memory.Read(imageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes)) throw new IOException("Practice flag instructions differ from the supported build.");
        if (!enabled) { RestorePracticeFlag(flag); return; }
        var player = ReadPlayer();
        var owner = flag == PracticeFlag.GodMode ? player.Component : imageBase;
        var address = FlagAddress(flag, owner);
        var mask = flag == PracticeFlag.GodMode ? (byte)3 : (byte)1;
        if (ownedFlags.TryGetValue(flag, out var previous) && previous.Owner != owner)
        {
            ownedFlags.Remove(flag);
            throw new IOException("Player changed. Enable the practice flag again after loading.");
        }
        var current = memory.Read(address, 1)[0];
        EnsureSamePlayer(player);
        memory.Write(address, [(byte)(current | mask)]);
        if (!ownedFlags.ContainsKey(flag)) ownedFlags[flag] = (owner, (byte)(current & mask));
    }
    public void RestorePracticeFlag(PracticeFlag flag)
    {
        if (!ownedFlags.TryGetValue(flag, out var previous)) return;
        if (flag == PracticeFlag.GodMode && ReadPlayer().Component != previous.Owner) { ownedFlags.Remove(flag); return; }
        var address = FlagAddress(flag, previous.Owner);
        var mask = flag == PracticeFlag.GodMode ? (byte)3 : (byte)1;
        var current = memory.Read(address, 1)[0];
        memory.Write(address, [(byte)((current & ~mask) | previous.Original)]);
        ownedFlags.Remove(flag);
    }
    public void RestorePracticeFlags()
    {
        var errors = new List<string>();
        foreach (var flag in ownedFlags.Keys.ToArray())
            try { RestorePracticeFlag(flag); } catch (IOException ex) { errors.Add(ex.Message); }
        if (errors.Count != 0) throw new IOException(string.Join(" ", errors.Distinct()));
    }
    private static ulong FlagAddress(PracticeFlag flag, ulong owner) => owner + (flag switch
    {
        PracticeFlag.GodMode => 0x200UL,
        PracticeFlag.OneShot => 0x553E889UL,
        PracticeFlag.Stealth => 0x553E88EUL,
        PracticeFlag.Silence => 0x553E88FUL,
        _ => throw new ArgumentException("Unknown practice flag.")
    });
    public ulong ImageBase => imageBase;
}
