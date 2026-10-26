using System.IO;

namespace BBTools.Memory;

public sealed partial class PlayerSession
{
    public static readonly (ulong Offset, byte[] Bytes) InsightFingerprint =
        (0x1901FB4, Convert.FromHexString("8BB384000000"));
    public int ReadInsight()
    {
        var f = InsightFingerprint;
        if (!memory.Read(imageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes))
            throw new IOException("Insight instructions differ from the supported build.");
        var character = ReadCharacter();
        var value = memory.Int32(character.Address + 0x84);
        if (value is < 0 or > 99) throw new IOException("Insight failed validation (expected 0–99).");
        EnsureSameCharacter(character);
        return value;
    }
    public void SetInsight(int value)
    {
        if (value is < 0 or > 99) throw new ArgumentException("Insight must be between 0 and 99.");
        ValidateImage();
        var character = ReadCharacter();
        ReadInsight();
        EnsureSameCharacter(character);
        memory.SetInt32(character.Address + 0x84, value);
        if (ReadInsight() != value || ReadCharacter().Address != character.Address)
            throw new IOException("Insight or the active character changed during the write. Check the game before retrying.");
    }
}
