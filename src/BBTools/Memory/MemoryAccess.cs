using System.IO;

namespace BBTools.Memory;

public interface IMemory
{
    byte[] Read(ulong address, int size);
    void Write(ulong address, byte[] value);
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
