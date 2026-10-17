using System.IO;

namespace BBTools.Memory;

public enum PracticeFlag { GodMode, OneShot, Stealth, Silence, NoAi, NoFallDamage, InfiniteItems }

public sealed partial class PlayerSession
{
    private readonly Dictionary<PracticeFlag, (ulong Owner, byte Original)> ownedFlags = [];
    public static readonly (ulong Offset, byte[] Bytes)[] FeatureFingerprints =
    [
        (0x1A2FD81, Convert.FromHexString("F6800002000002")),
        (0x1A57873, Convert.FromHexString("F6830002000001")),
        (0x19211E8, Convert.FromHexString("488D159AD6C103")),
        (0x19214BE, Convert.FromHexString("488D15C9D3C103")),
        (0x1921500, Convert.FromHexString("488D1588D3C103")),
        (0x1921374, Convert.FromHexString("488D151AD5C103")),
        (0x18F9814, Convert.FromHexString("0FB7920002000081E200020000")),
        (0x18F9F31, Convert.FromHexString("F6800102000002")),
        (0x1A35B75, Convert.FromHexString("F6800002000008"))
    ];
    public void SetPracticeFlag(PracticeFlag flag, bool enabled)
    {
        ValidateImage();
        foreach (var f in FeatureFingerprints)
            if (!memory.Read(imageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes)) throw new IOException("Practice flag instructions differ from the supported build.");
        if (!enabled) { RestorePracticeFlag(flag); return; }
        var player = ReadPlayer();
        var owner = LocalFlag(flag) ? player.Component : imageBase;
        var address = FlagAddress(flag, owner);
        var mask = FlagMask(flag);
        if (ownedFlags.TryGetValue(flag, out var previous) && previous.Owner != owner)
        {
            // The old component is no longer ours to touch. Capture the new owner's bits below.
            ownedFlags.Remove(flag);
        }
        var current = memory.Read(address, 1)[0];
        EnsureSamePlayer(player);
        memory.Write(address, [(byte)(current | mask)]);
        if (!ownedFlags.ContainsKey(flag)) ownedFlags[flag] = (owner, (byte)(current & mask));
    }
    public void RestorePracticeFlag(PracticeFlag flag)
    {
        if (!ownedFlags.TryGetValue(flag, out var previous)) return;
        if (LocalFlag(flag) && ReadPlayer().Component != previous.Owner) { ownedFlags.Remove(flag); return; }
        var address = FlagAddress(flag, previous.Owner);
        var mask = FlagMask(flag);
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
        PracticeFlag.NoAi => 0x553E895UL,
        PracticeFlag.NoFallDamage => 0x200UL,
        PracticeFlag.InfiniteItems => 0x201UL,
        _ => throw new ArgumentException("Unknown practice flag.")
    });
    private static bool LocalFlag(PracticeFlag flag) => flag is PracticeFlag.GodMode or PracticeFlag.NoFallDamage or PracticeFlag.InfiniteItems;
    private static byte FlagMask(PracticeFlag flag) => flag switch { PracticeFlag.GodMode => 3, PracticeFlag.NoFallDamage => 8, PracticeFlag.InfiniteItems => 2, _ => 1 };
    public ulong ImageBase => imageBase;
}
