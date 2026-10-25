using System.Diagnostics;
using BBTools.Memory;

namespace BBTools.LinuxCore;

public static class LinuxImageLocator
{
    public static ulong Find(IMemory memory, IEnumerable<LinuxMap> maps, TimeSpan budget, CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        var anchor = PlayerSession.Fingerprints[0];
        var candidates = new HashSet<ulong>();
        var scanned = 0UL;
        const int chunkSize = 1024 * 1024;
        // Locate an instruction anchor, not the host ELF base: bbport maps the
        // guest image separately and may split/coalesce its anonymous mappings.
        foreach (var map in maps.Where(m => m.ScanCode && m.End <= 0x800000000000))
        {
            for (var at = map.Start; at < map.End;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (clock.Elapsed > budget || scanned >= 512UL * 1024 * 1024)
                    throw new IOException("Linux image scan reached its time/size limit. No game writes were performed.");
                var length = (int)Math.Min((ulong)chunkSize, map.End - at);
                byte[] block;
                try { block = memory.Read(at, length); }
                catch (IOException) { at += (ulong)length; continue; }
                scanned += (ulong)length;
                for (var offset = 0; offset <= block.Length - anchor.Bytes.Length;)
                {
                    var index = block.AsSpan(offset).IndexOf(anchor.Bytes);
                    if (index < 0) break;
                    offset += index;
                    var address = at + (ulong)offset;
                    if (address >= anchor.Offset) candidates.Add(address - anchor.Offset);
                    offset++;
                }
                if (length < chunkSize) break;
                at += (ulong)(length - anchor.Bytes.Length + 1);
            }
        }
        return PlayerSession.FindImage(memory, candidates);
    }
}
