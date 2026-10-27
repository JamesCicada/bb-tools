using System.IO;

namespace BBTools.Memory;

public sealed record NgCycleState(ulong Manager, ulong Character, int Value);

public sealed partial class PlayerSession
{
    public static readonly (ulong Offset, byte[] Bytes)[] NgCycleFingerprints =
    [
        (0x131D250, Convert.FromHexString("488D05D9DE2104488B008B4068C3")),
        (0x157F130, Convert.FromHexString("488D05F9BFFB03488B008B406883F806B9060000000F4EC831C085C94863C9480F49C1"))
    ];
    public NgCycleState ReadNgCycle()
    {
        ValidateImage();
        foreach (var f in NgCycleFingerprints)
            if (!memory.Read(imageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes))
                throw new IOException("NG cycle instructions differ from the supported build.");
        var character = ReadCharacter(); var manager = memory.Pointer(imageBase + 0x553B130);
        if (memory.Pointer(manager + 8) != character.Address) throw new IOException("NG cycle character ownership changed.");
        var value = memory.Int32(manager + 0x68);
        if (value is < 0 or > 999) throw new IOException("NG cycle failed validation (expected 0–999).");
        EnsureSameCharacter(character);
        if (memory.Pointer(imageBase + 0x553B130) != manager || memory.Pointer(manager + 8) != character.Address || memory.Int32(manager + 0x68) != value)
            throw new IOException("NG cycle changed while reading. Reload the current value after loading finishes.");
        return new(manager, character.Address, value);
    }
    public void SetNgCycle(int value, NgCycleState expected)
    {
        if (value is < 0 or > 999) throw new ArgumentException("NG cycle must be between 0 and 999 (0 = NG, 1 = NG+).");
        void Apply()
        {
            var current = ReadNgCycle();
            if (current != expected) throw new IOException("Character or NG cycle changed. Reload the current value before applying.");
            memory.SetInt32(current.Manager + 0x68, value);
            if (ReadNgCycle() != (current with { Value = value })) throw new IOException("NG cycle could not be verified. Check the game before retrying.");
        }
        // Only the four-byte clear counter changes. Never call the native
        // next-journey routine, modify ending flags or alter player attributes.
        if (memory is ProcessMemory process) process.Suspended(Apply); else Apply();
    }
}
