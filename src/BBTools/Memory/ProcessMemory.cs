using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BBTools.Memory;

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
    public IEnumerable<ulong> ImageCandidates(bool emulator)
    {
        if(!emulator){foreach(var at in AllocationBases())yield return at;yield break;}
        var seen=new HashSet<ulong>();ulong address=0;
        while(address<0x0000800000000000&&VirtualQueryEx(handle,(nuint)address,out var region,(nuint)Marshal.SizeOf<MemoryInfo>())!=0)
        {
            if(region.State==0x1000&&region.Type is 0x20000 or 0x40000)
            {
                var start=(ulong)region.AllocationBase;
                if(start>=0x10000&&seen.Add(start))yield return start;
                var relocated=start+0x400000;
                if(relocated>=0x10000&&seen.Add(relocated))yield return relocated;
            }
            var next=(ulong)region.BaseAddress+(ulong)region.RegionSize;if(next<=address)break;address=next;
        }
    }
    public void Dispose() => handle.Dispose();
    internal ulong FindActiveCameraStep(ulong owner,ulong imageBase,CancellationToken cancellationToken=default)
    {
        if(VirtualQueryEx(handle,(nuint)owner,out var ownerRegion,(nuint)Marshal.SizeOf<MemoryInfo>())==0)
            throw new IOException("Active camera allocation is unavailable.");
        var allocation=(ulong)ownerRegion.AllocationBase;var at=allocation;
        var deadline=System.Diagnostics.Stopwatch.StartNew();
        var needle=BitConverter.GetBytes(imageBase+0x5330480);
        while(at<0x0000800000000000&&deadline.Elapsed<TimeSpan.FromSeconds(15))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(VirtualQueryEx(handle,(nuint)at,out var region,(nuint)Marshal.SizeOf<MemoryInfo>())==0||(ulong)region.AllocationBase!=allocation)break;
            var end=(ulong)region.BaseAddress+(ulong)region.RegionSize;if(end<=at)break;
            if(region.State==0x1000&&(region.Protect&0x100)==0&&(region.Protect&0xff) is 0x04 or 0x08)
            {
                for(var block=at;block<end;)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if(deadline.Elapsed>=TimeSpan.FromSeconds(15))break;
                    var size=(int)Math.Min(1024UL*1024,end-block);byte[] data;
                    try{data=Read(block,size);}catch(IOException){block+=(ulong)size;continue;}
                    var offset=0;
                    while(offset<=data.Length-8)
                    {
                        var found=data.AsSpan(offset).IndexOf(needle);if(found<0)break;
                        offset+=found;var candidate=block+(ulong)offset;
                        if(candidate%8==0&&FreeCamera.MatchesCameraStep(this,imageBase,candidate,owner))return candidate;
                        offset+=8;
                    }
                    block+=(ulong)size;
                }
            }
            at=end;
        }
        throw new IOException("The active native camera task was not found in its game allocation. Photo mode was not enabled.");
    }
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
    // Narrow exception to data-only writes: one verified FOV clamp instruction.
    // Called only while the target threads are suspended by CameraFov.
    internal void WriteCameraFovClamp(ulong imageBase, bool enable)
    {
        foreach (var fingerprint in CameraFov.Fingerprints)
            if (!Read(imageBase + fingerprint.Offset, fingerprint.Bytes.Length).SequenceEqual(fingerprint.Bytes))
                throw new IOException("Camera FOV instructions changed; the clamp was not modified.");
        var address = imageBase + CameraFov.ClampOffset;
        var expected = enable ? CameraFov.StandardClamp : CameraFov.UncappedClamp;
        var replacement = enable ? CameraFov.UncappedClamp : CameraFov.StandardClamp;
        if (!Read(address,4).SequenceEqual(expected)) throw new IOException("Camera FOV clamp changed before the update.");
        if (!VirtualQueryEx(handle,(nuint)address,out var region,(nuint)Marshal.SizeOf<MemoryInfo>()).Equals((nuint)Marshal.SizeOf<MemoryInfo>()) ||
            region.State != 0x1000 || (region.Protect & 0x100) != 0 || address+4 > (ulong)region.BaseAddress+(ulong)region.RegionSize)
            throw new IOException("Camera clamp is not in a valid committed memory region.");
        if (!VirtualProtectEx(handle,(nuint)address,4,0x40,out var oldProtection))
            throw new IOException("Could not temporarily update camera clamp protection.");
        try
        {
            if (!WriteProcessMemory(handle,(nuint)address,replacement,4,out var written) || written != 4)
                throw new IOException("Camera clamp write failed.");
        }
        finally
        {
            var flushed=FlushInstructionCache(handle,(nuint)address,4);
            var restored=VirtualProtectEx(handle,(nuint)address,4,oldProtection,out _);
            if (!flushed || !restored) throw new IOException("Camera clamp cache/protection restoration failed.");
        }
        if (!Read(address,4).SequenceEqual(replacement)) throw new IOException("Camera clamp update could not be verified.");
    }
    // Only the independently verified movement read site may use this hook path.
    internal void ReplaceMovementCode(ulong imageBase,byte[] expected,byte[] replacement)
    {
        if(expected.Length!=14||replacement.Length!=14)throw new ArgumentException("Invalid movement patch size.");
        var at=imageBase+MovementSpeed.Offset;
        if(!Read(at,14).SequenceEqual(expected))throw new IOException("Movement instructions changed externally; no patch was applied.");
        if(!VirtualProtectEx(handle,(nuint)at,14,0x40,out var protection))throw new IOException("Movement code is unavailable.");
        try{if(!WriteProcessMemory(handle,(nuint)at,replacement,14,out var count)||count!=14)throw new IOException("Movement patch write failed.");}
        finally{var flushed=FlushInstructionCache(handle,(nuint)at,14);var restored=VirtualProtectEx(handle,(nuint)at,14,protection,out _);if(!flushed||!restored)throw new IOException("Movement protection/cache restoration failed.");}
    }
    // Only the two independently verified loot sites may use this write path.
    internal void ReplaceLootCode(ulong imageBase, ulong offset, byte[] expected, byte[] replacement)
    {
        var original = offset == LootOverrides.DiscoveryOffset ? LootOverrides.DiscoveryOriginal :
            offset == LootOverrides.DropOffset ? LootOverrides.DropOriginal : throw new ArgumentException("Unsupported loot hook site.");
        if (expected.Length != original.Length || replacement.Length != original.Length)
            throw new ArgumentException("Invalid loot patch length.");
        var at = imageBase + offset;
        if (!Read(at, expected.Length).SequenceEqual(expected)) throw new IOException("Loot instructions changed externally; no patch was applied.");
        if (!VirtualProtectEx(handle, (nuint)at, (nuint)expected.Length, 0x40, out var protection)) throw new IOException("Loot code is unavailable.");
        try
        {
            if (!WriteProcessMemory(handle, (nuint)at, replacement, (nuint)replacement.Length, out var count) || count != (nuint)replacement.Length)
                throw new IOException("Loot patch write failed.");
        }
        finally
        {
            var flushed = FlushInstructionCache(handle, (nuint)at, (nuint)replacement.Length);
            var restored = VirtualProtectEx(handle, (nuint)at, (nuint)replacement.Length, protection, out _);
            if (!flushed || !restored) throw new IOException("Loot code protection/cache restoration failed.");
        }
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
