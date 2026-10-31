using System.IO;

namespace BBTools.Memory;

public static class GameDiagnostics
{
    public static Action<string,string>? Log { get; set; }
}

public interface IMemory
{
    byte[] Read(ulong address, int size);
    void Write(ulong address, byte[] value);
}

// Optional code-page inspection for compatibility adapters; ordinary memory
// readers/writers do not acquire permission to accept redirected instructions.
public interface IExecutableMemory : IMemory
{
    bool IsExecutable(ulong address, int size);
}

// Platform operations used by shared game features. Protected writes remain
// narrow named operations; this is not a general executable-memory write API.
public interface IGameMemory : IExecutableMemory, IDisposable
{
    bool IsAlive { get; }
    ulong AllocateData(int size);
    ulong AllocateCode(byte[] code);
    void Suspended(Action operation);
    ulong FindActiveCameraStep(ulong owner, ulong imageBase, CancellationToken cancellationToken = default);
    void WriteCameraFovClamp(ulong imageBase, bool enable);
    void ReplaceMovementCode(ulong imageBase, byte[] expected, byte[] replacement);
    void ReplacePhotoOutputCode(ulong imageBase, byte[] expected, byte[] replacement);
    void ReplaceShoulderCode(ulong imageBase, byte[] expected, byte[] replacement);
    void ReplaceLootCode(ulong imageBase, ulong offset, byte[] expected, byte[] replacement);
    void ReplaceProgressionCode(ulong imageBase, ulong offset, byte[] expected, byte[] replacement);
}

public static class MemoryValues
{
    public static int Int32(this IMemory memory, ulong address) => BitConverter.ToInt32(memory.Read(address, 4));
    public static ulong Pointer(this IMemory memory, ulong address, string context = "game object")
    {
        var value = BitConverter.ToUInt64(memory.Read(address, 8));
        if (value < 0x10000 || value >= 0x0000800000000000 || value % 8 != 0)
            throw new IOException($"Unavailable {context} pointer at 0x{address:X} (value 0x{value:X}).");
        return value;
    }
    public static void SetInt32(this IMemory memory, ulong address, int value) => memory.Write(address, BitConverter.GetBytes(value));
}
