namespace BBTools.Memory;

// The normal reader only consults GameDataMan+0xA8. Some bbport-derived
// runtimes clear that field during the death transition, after the trainer has
// set it but before the game commits the bloodstain. This tightly-scoped reader
// override makes the native query return true while Echoes protection is owned.
public static class DeathPenaltyOverride
{
    public const ulong Offset = 0x13389B0;
    public static readonly byte[] Original = Convert.FromHexString("488D0579272004488B004885C0740B80B8A8000000000F95C0C3");
    public static readonly byte[] Forced = [0xB8,0x01,0x00,0x00,0x00,0xC3,..Enumerable.Repeat((byte)0x90,Original.Length-6)];
    public static bool IsSupportedPair(byte[] expected,byte[] replacement) =>
        expected.SequenceEqual(Original) && replacement.SequenceEqual(Forced) ||
        expected.SequenceEqual(Forced) && replacement.SequenceEqual(Original);
}
