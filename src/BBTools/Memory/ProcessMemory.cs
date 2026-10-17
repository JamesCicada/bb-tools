using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BBTools.Memory;

public interface IMemory
{
    byte[] Read(ulong address, int size);
    void Write(ulong address, byte[] value);
}

public static class MemoryValues
{
    public static int Int32(this IMemory memory, ulong address) => BitConverter.ToInt32(memory.Read(address, 4));
    public static ulong Pointer(this IMemory memory, ulong address)
    {
        var value = BitConverter.ToUInt64(memory.Read(address, 8));
        if (value < 0x10000 || value >= 0x0000800000000000 || value % 8 != 0)
            throw new IOException("Player is unavailable. Load a character and try again.");
        return value;
    }
    public static void SetInt32(this IMemory memory, ulong address, int value) => memory.Write(address, BitConverter.GetBytes(value));
}

public sealed class ProcessMemory : IMemory, IDisposable
{
    private readonly SafeProcessHandle handle;
    public ProcessMemory(int pid, bool writable = true)
    {
        handle = OpenProcess(0x100000 | 0x400 | 0x10 | (writable ? 0x800u | 0x20u | 0x08u : 0), false, pid);
        if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
    }
    public bool IsAlive => WaitForSingleObject(handle, 0) == 0x102;
    public byte[] Read(ulong address, int size)
    {
        var bytes = new byte[size];
        if (!ReadProcessMemory(handle, (nuint)address, bytes, (nuint)size, out var read) || read != (nuint)size)
            throw new IOException($"Cannot read game memory at 0x{address:X}. The player may be loading.");
        return bytes;
    }
    public void Write(ulong address, byte[] value)
    {
        if (!IsAlive) throw new IOException("The game process has exited.");
        if (!VirtualQueryEx(handle, (nuint)address, out var region, (nuint)Marshal.SizeOf<MemoryInfo>()).Equals((nuint)Marshal.SizeOf<MemoryInfo>())
            || region.State != 0x1000 || (region.Protect & 0x100) != 0
            || (region.Protect & 0xff) is not (0x04 or 0x08) // Data pages only; never executable memory.
            || address + (ulong)value.Length > (ulong)region.BaseAddress + (ulong)region.RegionSize)
            throw new IOException("Target is not writable game data. No write was performed.");
        if (!WriteProcessMemory(handle, (nuint)address, value, (nuint)value.Length, out var written) || written != (nuint)value.Length)
            throw new IOException("The game memory write failed.");
    }
    public IEnumerable<ulong> AllocationBases()
    {
        var seen = new HashSet<ulong>();
        ulong address = 0;
        while (address < 0x0000800000000000 && VirtualQueryEx(handle, (nuint)address, out var region, (nuint)Marshal.SizeOf<MemoryInfo>()) != 0)
        {
            if (region.State == 0x1000 && region.Type == 0x20000 && seen.Add((ulong)region.AllocationBase))
                yield return (ulong)region.AllocationBase;
            var next = (ulong)region.BaseAddress + (ulong)region.RegionSize;
            if (next <= address) break;
            address = next;
        }
    }
    public void Dispose() => handle.Dispose();
    public ulong AllocateData(int size)
    {
        var address = VirtualAllocEx(handle, 0, (nuint)size, 0x3000, 4);
        if (address == 0) throw new IOException("Could not allocate the game request workspace.");
        return (ulong)address;
    }
    public ulong AllocateCode(byte[] code)
    {
        var address = AllocateData(code.Length);
        Write(address, code);
        if (!VirtualProtectEx(handle, (nuint)address, (nuint)code.Length, 0x20, out _) || !FlushInstructionCache(handle, (nuint)address, (nuint)code.Length))
            throw new IOException("Could not finalize the game-thread dispatcher.");
        return address;
    }
    public void Suspended(Action operation)
    {
        if (!IsAlive || NtSuspendProcess(handle) != 0) throw new IOException("Could not pause game threads for the request update.");
        try { operation(); }
        finally { if (NtResumeProcess(handle) != 0 && IsAlive) throw new IOException("Could not resume game threads."); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryInfo
    {
        public nuint BaseAddress, AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nuint RegionSize;
        public uint State, Protect, Type;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] private static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadProcessMemory(SafeProcessHandle process, nuint address, [Out] byte[] buffer, nuint size, out nuint read);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteProcessMemory(SafeProcessHandle process, nuint address, byte[] buffer, nuint size, out nuint written);
    [DllImport("kernel32.dll")] private static extern nuint VirtualQueryEx(SafeProcessHandle process, nuint address, out MemoryInfo info, nuint size);
    [DllImport("kernel32.dll")] private static extern nuint VirtualAllocEx(SafeProcessHandle process, nuint address, nuint size, uint allocation, uint protection);
    [DllImport("kernel32.dll")] private static extern bool VirtualProtectEx(SafeProcessHandle process, nuint address, nuint size, uint protection, out uint old);
    [DllImport("kernel32.dll")] private static extern bool FlushInstructionCache(SafeProcessHandle process, nuint address, nuint size);
    [DllImport("ntdll.dll")] private static extern int NtSuspendProcess(SafeProcessHandle process);
    [DllImport("ntdll.dll")] private static extern int NtResumeProcess(SafeProcessHandle process);
}
