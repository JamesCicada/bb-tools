using System.IO;

namespace BBTools.Memory;

public enum PracticeFlag { GodMode, OneShot, Stealth, Silence, NoAi, NoFallDamage, InfiniteItems,NoHitReaction,NoEchoesLoss }

public sealed partial class PlayerSession
{
    private readonly Dictionary<PracticeFlag, (ulong Owner, byte Original,uint Handle)> ownedFlags = [];
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
        (0x1A35B75, Convert.FromHexString("F6800002000008")),
        (0x1332F13, Convert.FromHexString("0FB6CB8B90E10100000FB6B0E5010000")),
        (0x18CBFC7, Convert.FromHexString("8BB7E1010000F7C600400004")),
        (0x13389B0, Convert.FromHexString("488D0579272004488B004885C0740B80B8A8000000000F95C0C3"))
    ];
    public void SetPracticeFlag(PracticeFlag flag, bool enabled)
    {
        ValidateImage();
        foreach (var f in FeatureFingerprints)
            if (!memory.Read(imageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes)) throw new IOException("Practice flag instructions differ from the supported build.");
        if (!enabled) { RestorePracticeFlag(flag); return; }
        var player = ReadPlayer();
        var owner = FlagOwner(flag,player);
        var handle=LocalFlag(flag)?unchecked((uint)memory.Int32(player.Actor+8)):0;
        var address = FlagAddress(flag, owner);
        var mask = FlagMask(flag);
        if (ownedFlags.TryGetValue(flag, out var previous) && (previous.Owner != owner||previous.Handle!=handle))
        {
            // The old component is no longer ours to touch. Capture the new owner's bits below.
            ownedFlags.Remove(flag);
        }
        var current = memory.Read(address, 1)[0];
        if(flag==PracticeFlag.NoEchoesLoss&&current>1)throw new IOException("Native death-penalty flag differs from the supported layout.");
        EnsureSamePlayer(player);
        if(LocalFlag(flag)&&unchecked((uint)memory.Int32(player.Actor+8))!=handle)throw new IOException("Practice flag owner changed.");
        if (!ownedFlags.ContainsKey(flag)) ownedFlags[flag] = (owner, (byte)(current & mask),handle);
        // Polling keeps selections across loading, but a stable engine flag needs
        // no write. Preserve ownership even if the original bit was already on.
        var desired=(byte)(current|mask);
        if(current!=desired)memory.Write(address,[desired]);
    }
    public void RestorePracticeFlag(PracticeFlag flag)
    {
        if (!ownedFlags.TryGetValue(flag, out var previous)) return;
        if(flag==PracticeFlag.NoEchoesLoss&&memory.Pointer(imageBase+0x553B130)!=previous.Owner){ownedFlags.Remove(flag);return;}
        if (LocalFlag(flag))
        {
            var player=ReadPlayer();
            if(FlagOwner(flag,player)!=previous.Owner||unchecked((uint)memory.Int32(player.Actor+8))!=previous.Handle){ownedFlags.Remove(flag);return;}
        }
        var address = FlagAddress(flag, previous.Owner);
        var mask = FlagMask(flag);
        var current = memory.Read(address, 1)[0];
        var desired=(byte)((current&~mask)|previous.Original);
        if(current!=desired)memory.Write(address,[desired]);
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
        PracticeFlag.NoHitReaction=>0x1E2UL,
        PracticeFlag.NoEchoesLoss=>0xA8UL,
        _ => throw new ArgumentException("Unknown practice flag.")
    });
    private ulong FlagOwner(PracticeFlag flag,PlayerState player)=>flag switch{PracticeFlag.NoHitReaction=>player.Actor,PracticeFlag.NoEchoesLoss=>memory.Pointer(imageBase+0x553B130),_=>LocalFlag(flag)?player.Component:imageBase};
    private static bool LocalFlag(PracticeFlag flag) => flag is PracticeFlag.GodMode or PracticeFlag.NoFallDamage or PracticeFlag.InfiniteItems or PracticeFlag.NoHitReaction;
    private static byte FlagMask(PracticeFlag flag) => flag switch { PracticeFlag.GodMode => 3, PracticeFlag.NoFallDamage => 8, PracticeFlag.InfiniteItems => 2,PracticeFlag.NoHitReaction=>0x40, _ => 1 };
    public ulong ImageBase => imageBase;
}
