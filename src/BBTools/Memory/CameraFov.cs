using System.IO;
using System.Text;
namespace BBTools.Memory;

// Parameter override plus an owned, reversible four-byte runtime clamp change.
public sealed class CameraFov(IMemory memory, PlayerSession session)
{
    public const ulong ClampOffset = 0x143AF4E;
    public static readonly byte[] StandardClamp = Convert.FromHexString("480F46C6");
    public static readonly byte[] UncappedClamp = Convert.FromHexString("4889F090");
    public static readonly (ulong Offset, byte[] Bytes)[] Fingerprints =
    [
        (0x143AF1E, Convert.FromHexString("C5FA104E14C5FA101535AF4E03")),
        (0x143AF5E, Convert.FromHexString("C4C17A105550C5F25CCAC5F259CCC5EA58C9"))
    ];
    private readonly Dictionary<ulong, (float Original, float Written)> owned = new();
    private ulong table;
    private bool ownsUncap;
    public float Maximum
    {
        get
        {
            foreach (var f in Fingerprints)
                if (!memory.Read(session.ImageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes)) throw new IOException("Camera FOV reader differs from the supported build.");
            var bytes = memory.Read(session.ImageBase + ClampOffset, 4);
            if (bytes.SequenceEqual(StandardClamp)) return 120;
            if (bytes.SequenceEqual(UncappedClamp)) return 120;
            throw new IOException("Camera FOV instructions differ from the supported build.");
        }
    }
    internal (ulong File, ulong[] Fields) Resolve(bool requirePlayer = true)
    {
        session.ValidateImage(); if(requirePlayer) session.ReadPlayer();
        var repo = memory.Pointer(session.ImageBase + 0x5540340);
        var slot = repo + 0x70 + 0x21 * 0x48;
        if (memory.Int32(slot) != 1) throw new IOException("A unique camera parameter table is not loaded.");
        var cap = memory.Pointer(slot + 8);
        var name = memory.Pointer(cap + 0x18);
        if (!Encoding.Unicode.GetString(memory.Read(name, 26)).Equals("LockCamParam\0",StringComparison.Ordinal))
            throw new IOException("Camera parameter resource name differs from the supported build.");
        var file = memory.Pointer(memory.Pointer(cap + 0x70) + 0x70);
        var header = memory.Read(file,0x40);
        if (!Encoding.ASCII.GetString(header,12,18).Equals("LOCK_CAM_PARAM_ST\0",StringComparison.Ordinal))
            throw new IOException("Camera parameter type failed validation.");
        var count = BitConverter.ToUInt16(header,10);
        if (count is < 2 or > 4096) throw new IOException("Invalid camera row count.");
        var format = header[0x2D]; var flags = header[0x2E];
        var wide = format > 3 && (flags & 2) != 0;
        var start = format == 2 ? 0x34 : 0x40; var stride = wide ? 24 : 12;
        var records = memory.Read(file + (ulong)start,count*stride);
        var offsets = new List<ulong>(); var ids = new HashSet<int>();
        for (var i=0;i<count;i++)
        {
            var at=i*stride;
            var offset=wide ? BitConverter.ToUInt64(records,at+8) : BitConverter.ToUInt32(records,at+4);
            if (!ids.Add(BitConverter.ToInt32(records,at)) || offset < (ulong)(start+count*stride) || offset > 0x1000000 || offset%4 != 0)
                throw new IOException("Invalid camera parameter records.");
            offsets.Add(offset);
        }
        if (offsets.Zip(offsets.Skip(1)).Any(x => x.Second-x.First != 0x20)) throw new IOException("Camera row size differs from the supported layout.");
        return (file, offsets.Select(x=>file+x+0x14).ToArray());
    }
    public void Apply(float degrees)
    {
        if (!float.IsFinite(degrees) || degrees < 38 || degrees > Maximum) throw new ArgumentException($"Vertical FOV must be between 38 and {Maximum:0} degrees.");
        var current = Resolve();
        // Normal polling only reads. Pause threads when an angle or table changes.
        if ((degrees <= 48 || memory.Read(session.ImageBase+ClampOffset,4).SequenceEqual(UncappedClamp)) &&
            table == current.File && owned.Count == current.Fields.Length && current.Fields.All(at =>
            owned.TryGetValue(at,out var entry) && entry.Written == degrees && BitConverter.ToSingle(memory.Read(at,4)) == degrees)) return;
        if (memory is IGameMemory process) process.Suspended(() => ApplyCore(degrees));
        else ApplyCore(degrees);
    }
    private void ApplyCore(float degrees)
    {
        if (!float.IsFinite(degrees) || degrees < 38 || degrees > Maximum) throw new ArgumentException($"Vertical FOV must be between 38 and {Maximum:0} degrees.");
        var current = Resolve();
        // A new table belongs to a new load; do not write through old allocations.
        if (table != current.File) { owned.Clear(); table=current.File; }
        var values = current.Fields.Select(at=>(At:at,Value:BitConverter.ToSingle(memory.Read(at,4)))).ToArray();
        foreach(var x in values)
        {
            if (!float.IsFinite(x.Value) || x.Value is <=1 or >=179) throw new IOException("Camera row FOV failed validation.");
            if (owned.TryGetValue(x.At,out var entry) && x.Value != entry.Written)
                throw new IOException("Another tool changed camera FOV. Disable the override before retrying.");
        }
        if (Resolve().File != table) throw new IOException("Camera table changed before applying FOV.");
        if (degrees > 48 && memory.Read(session.ImageBase+ClampOffset,4).SequenceEqual(StandardClamp))
        {
            if (memory is not IGameMemory process) throw new IOException("Wide FOV requires native process memory access.");
            // Record ownership first so cleanup can recover even after a write/verification failure.
            ownsUncap = true;
            process.WriteCameraFovClamp(session.ImageBase,true);
        }
        foreach(var x in values)
        {
            if (!owned.TryGetValue(x.At,out var entry)) entry=(x.Value,x.Value);
            if(x.Value != degrees) memory.Write(x.At,BitConverter.GetBytes(degrees));
            owned[x.At]=(entry.Original,degrees);
        }
    }
    public void Restore()
    {
        if (owned.Count==0 && !ownsUncap) return;
        if (memory is IGameMemory process) process.Suspended(RestoreCore);
        else RestoreCore();
    }
    private void RestoreCore()
    {
        try
        {
            if (owned.Count==0) return;
            if (Resolve(false).File != table) { owned.Clear(); table=0; return; }
            foreach(var (at,entry) in owned.ToArray())
            {
                if(BitConverter.ToSingle(memory.Read(at,4)) == entry.Written) memory.Write(at,BitConverter.GetBytes(entry.Original));
                owned.Remove(at);
            }
            table=0;
        }
        finally
        {
            if (ownsUncap)
            {
                var bytes=memory.Read(session.ImageBase+ClampOffset,4);
                if (bytes.SequenceEqual(UncappedClamp))
                {
                    if (memory is not IGameMemory process) throw new IOException("Cannot restore native camera clamp.");
                    process.WriteCameraFovClamp(session.ImageBase,false);
                }
                else if (!bytes.SequenceEqual(StandardClamp)) throw new IOException("Camera clamp changed externally; unknown instructions were retained.");
                ownsUncap=false;
            }
        }
    }
}
