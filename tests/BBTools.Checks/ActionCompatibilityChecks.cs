using System.IO;
using BBTools.Memory;

internal static partial class Program
{
    private sealed class ActionProfileMemory(ulong root) : IMemory
    {
        public readonly Dictionary<ulong, byte[]> Instructions = GameActions.Fingerprints
            .ToDictionary(f => root + f.Offset, f => f.Bytes.ToArray());
        public byte[] Read(ulong address, int size) => Instructions[address].Take(size).ToArray();
        public void Write(ulong address, byte[] value) => throw new Exception("Compatibility inspection must not write.");
    }

    private static void ActionCompatibilityChecks()
    {
        const ulong root = 0x800000000;
        var source = new ActionProfileMemory(root);
        var kinds = Enum.GetValues<GameActionKind>();
        foreach (var kind in kinds)
        {
            GameActions.ValidateActionInstructions(source, root, kind);
            Check(true, $"original 1.09 instructions validate for {kind}");
        }
        foreach (var f in GameActions.Fingerprints)
        {
            var bytes = source.Instructions[root + f.Offset];
            bytes[0] ^= 0xFF;
            var failures = new HashSet<GameActionKind>();
            foreach (var kind in kinds)
            {
                try { GameActions.ValidateActionInstructions(source, root, kind); }
                catch (IOException) { failures.Add(kind); }
            }
            var expected = f.Offset switch
            {
                0x18FD960 or 0x18F7891 => kinds,
                0x154B110 => [GameActionKind.RestoreLocation],
                0x156D110 or 0x156D0E0 or 0x132E050 => [GameActionKind.Warp],
                _ => new[] { GameActionKind.Item }
            };
            Check(failures.SetEquals(expected), $"mismatch at 0x{f.Offset:X} blocks every dependent action and preserves unrelated actions");
            bytes[0] ^= 0xFF;
        }
        source.Instructions[root + 0x17D89F0][0] = 0xE9;
        source.Instructions[root + 0x1A86FF0][0] = 0xCC;
        try
        {
            GameActions.ValidateActionInstructions(source, root, GameActionKind.Item);
            throw new Exception("Modified item code was accepted.");
        }
        catch (IOException ex)
        {
            Check(ex.Message.Contains("image+0x17D89F0") && ex.Message.Contains("image+0x1A86FF0") &&
                ex.Message.Contains("0x8017D89F0") && ex.Message.Contains("expected 554889") && ex.Message.Contains("actual E94889"),
                "compatibility error identifies all relevant mismatches with absolute/relative addresses and expected/actual bytes");
            Check(ex.Message.Contains("No request was submitted"), "compatibility failure explains that no item was granted");
        }
        Reject<ArgumentOutOfRangeException>(() => GameActions.ValidateActionInstructions(source, root, (GameActionKind)999),
            "unknown action kinds cannot skip validation");
        var spear = GameActions.CommonItems.Single(x => x.Name == "Saw Spear");
        var batch = GameActions.ItemBatch(spear.Id, spear.Category, 1);
        Check(spear.Id == 7100000 && spear.Category == 0 && BitConverter.ToUInt32(batch, 8) == 7100000 &&
            BitConverter.ToInt32(batch, 12) == 1, "Saw Spear uses decimal 7100000, Weapon category and one equipment instance");
    }
}
