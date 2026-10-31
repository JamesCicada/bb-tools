using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using BBTools.Memory;

namespace BBTools.LinuxCore;

public sealed record LinuxMap(ulong Start, ulong End, string Permissions, string Path)
{
    public bool ScanCode => Permissions[0] == 'r' && Permissions[2] == 'x';
    public static LinuxMap Parse(string line)
    {
        var fields = line.Split((char[]?)null, 6, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 5) throw new IOException("Invalid Linux process map.");
        var range = fields[0].Split('-');
        if (range.Length != 2 ||
            !ulong.TryParse(range[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var start) ||
            !ulong.TryParse(range[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var end) || end <= start ||
            fields[1].Length != 4 || fields[1][0] is not ('r' or '-') || fields[1][1] is not ('w' or '-') ||
            fields[1][2] is not ('x' or '-') || fields[1][3] is not ('p' or 's'))
            throw new IOException("Invalid Linux process map.");
        return new(start, end, fields[1], fields.Length == 6 ? fields[5] : "");
    }
}

// Read-only unless the desktop explicitly enables experimental data editing.
// No ptrace attach, signals, remote allocations or process permission changes.
public sealed partial class LinuxProcessMemory : IGameMemory
{
    private readonly int pid;
    private readonly string identity;
    public bool WritesEnabled { get; set; }
    public int Pid => pid;
    public bool HasExited()
    {
        try
        {
            var stat=File.ReadAllText($"/proc/{pid}/stat");var end=stat.LastIndexOf(')');
            if(end<0)throw new IOException("Invalid Linux process identity.");
            var fields=stat[(end+1)..].Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
            if(fields.Length>0&&fields[0] is "Z" or "X")return true;
            return ParseIdentity(stat)!=identity;
        }
        catch(Exception ex)when(ex is FileNotFoundException or DirectoryNotFoundException){return true;}
    }
    public LinuxProcessMemory(int pid)
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("The connection probe requires native Linux x64.");
        if (pid <= 0) throw new ArgumentOutOfRangeException(nameof(pid));
        this.pid = pid;
        identity = Identity();
    }
    private string Identity()
    {
        var stat = File.ReadAllText($"/proc/{pid}/stat");
        return ParseIdentity(stat);
    }
    internal static string ParseIdentity(string stat)
    {
        // comm can contain spaces and parentheses. Field 22 is starttime, not PID.
        var end = stat.LastIndexOf(')');
        if (end < 0) throw new IOException("Invalid Linux process identity.");
        var fields = stat[(end + 1)..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length < 20 || fields[0] is "Z" or "X" || !ulong.TryParse(fields[19], out _))
            throw new IOException("Linux process exited or its identity is unavailable.");
        return fields[19];
    }
    public void ValidateIdentity()
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if (Identity() != identity) throw new IOException("The selected process exited or its PID was reused. Select it again.");
    }
    public LinuxMap[] Maps()
    {
        ValidateIdentity();
        var result = File.ReadLines($"/proc/{pid}/maps").Select(LinuxMap.Parse).ToArray();
        ValidateIdentity();
        return result;
    }
    public byte[] Read(ulong address, int size)
    {
        if (size is < 1 or > 1024 * 1024 || address >= 0x800000000000 || (ulong)size > 0x800000000000 - address)
            throw new ArgumentOutOfRangeException(nameof(size), "Invalid Linux read range.");
        ValidateIdentity();
        var bytes = new byte[size];
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var local = new IOVector(pinned.AddrOfPinnedObject(), (nuint)size);
            var remote = new IOVector((nint)address, (nuint)size);
            nint count;
            var retries = 0;
            do { count = ReadProcess(pid, ref local, 1, ref remote, 1, 0); }
            while (count == -1 && Marshal.GetLastPInvokeError() == 4 && retries++ < 8); // bounded EINTR
            if (count != size)
            {
                var error = count == -1 ? Marshal.GetLastPInvokeError() : 0;
                if (error is 1 or 13)
                    throw new UnauthorizedAccessException(LinuxPermissions.Explain());
                throw new IOException($"Linux memory read failed at 0x{address:X}: {(count == -1 ? new Win32Exception(error).Message : $"short read ({count}/{size})")}.");
            }
        }
        finally { pinned.Free(); }
        ValidateIdentity();
        return bytes;
    }
    public void Write(ulong address, byte[] value)
    {
        if (!WritesEnabled) throw new NotSupportedException("Linux memory writes are disabled. The connection probe is always read-only.");
        if (value.Length is < 1 or > 1024 * 1024 || address >= 0x800000000000 || (ulong)value.Length > 0x800000000000 - address)
            throw new ArgumentOutOfRangeException(nameof(value));
        var end = address + (ulong)value.Length;
        if (!Maps().Any(m => m.Start <= address && end <= m.End && m.Permissions[0] == 'r' && m.Permissions[1] == 'w' && m.Permissions[2] != 'x'))
            throw new IOException("Target is not one readable, writable, non-executable Linux data mapping. No write was performed.");
        ValidateIdentity();
        var pinned = GCHandle.Alloc(value, GCHandleType.Pinned);
        try
        {
            var local = new IOVector(pinned.AddrOfPinnedObject(), (nuint)value.Length);
            var remote = new IOVector((nint)address, (nuint)value.Length);
            nint count; var retries = 0;
            do { count = WriteProcess(pid, ref local, 1, ref remote, 1, 0); }
            while (count == -1 && Marshal.GetLastPInvokeError() == 4 && retries++ < 8);
            if (count != value.Length)
            {
                var error = count == -1 ? Marshal.GetLastPInvokeError() : 0;
                if (error is 1 or 13) throw new UnauthorizedAccessException(LinuxPermissions.Explain());
                throw new IOException($"Linux memory write failed ({count}/{value.Length}); check the game before retrying.");
            }
        }
        finally { pinned.Free(); }
        ValidateIdentity();
    }
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct IOVector(nint address, nuint length)
    {
        public readonly nint Address = address;
        public readonly nuint Length = length;
    }
    [DllImport("libc", EntryPoint = "process_vm_readv", SetLastError = true)]
    private static extern nint ReadProcess(int pid, ref IOVector local, nuint localCount, ref IOVector remote, nuint remoteCount, nuint flags);
    [DllImport("libc", EntryPoint = "process_vm_writev", SetLastError = true)]
    private static extern nint WriteProcess(int pid, ref IOVector local, nuint localCount, ref IOVector remote, nuint remoteCount, nuint flags);
}
