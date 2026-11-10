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
        var readFailures = 0;
        const int chunkSize = 1024 * 1024;
        // NVIDIA/LLVM/DLSS code alone can push a loaded bbport process past
        // 512 MiB. Keep a bounded full scan so later valid images still make
        // the result ambiguous instead of accepting the first anchor found.
        const ulong maxScanBytes = 1024UL * 1024 * 1024;
        var codeMaps = maps.Where(m => m.ScanCode && m.End <= 0x800000000000).ToArray();
        string Progress() => $"scanned={scanned / (1024.0 * 1024):F2} MiB, elapsed={clock.Elapsed.TotalSeconds:F2}s, candidates={candidates.Count}, failed reads={readFailures}";
        GameDiagnostics.Log?.Invoke("SCAN", $"Starting Linux image scan: {codeMaps.Length} code mappings, {codeMaps.Sum(m => (double)(m.End - m.Start)) / (1024 * 1024):F2} MiB; limit=1024 MiB, budget={budget.TotalSeconds:F2}s.");
        // Locate an instruction anchor, not the host ELF base: bbport maps the
        // guest image separately and may split/coalesce its anonymous mappings.
        foreach (var map in codeMaps)
        {
            for (var at = map.Start; at < map.End;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (clock.Elapsed > budget || scanned >= maxScanBytes)
                {
                    var reason = clock.Elapsed > budget ? "time" : "size";
                    throw new IOException($"Linux image scan reached its {reason} limit ({Progress()}). No game writes were performed.");
                }
                var length = (int)Math.Min((ulong)chunkSize, Math.Min(map.End - at, maxScanBytes - scanned));
                byte[] block;
                try { block = memory.Read(at, length); }
                catch (IOException) { readFailures++; at += (ulong)length; continue; }
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
                if ((ulong)length == map.End - at) break;
                if (length < chunkSize)
                    throw new IOException($"Linux image scan reached its size limit ({Progress()}). No game writes were performed.");
                at += (ulong)(length - anchor.Bytes.Length + 1);
            }
        }
        GameDiagnostics.Log?.Invoke("SCAN", $"Linux image scan complete: {Progress()}.");
        return PlayerSession.FindImage(memory, candidates);
    }
}
