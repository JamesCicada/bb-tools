using System.IO;
using System.Text;

namespace BBTools.Memory;

// Runtime-only cost changes. Equipment creation, upgrade limits and stat caps
// remain native. Never refill currencies, create materials or edit game files.
public sealed class ProgressionCosts(IGameMemory memory, PlayerSession session)
{
    public static readonly (ulong Offset, byte[] Original, byte[] Free)[] UpgradeSites =
    [
        (0x1B5F02F, Convert.FromHexString("8B5818"), Convert.FromHexString("31DB90")),
        (0x1B5F0BE, Convert.FromHexString("448B648DB0"), Convert.FromHexString("4531E49090"))
    ];
    public static readonly (ulong Offset, byte[] Bytes)[] Fingerprints =
    [
        (0x1B5EEC0, Convert.FromHexString("554889E54157415641554154534881ECC8000000")),
        (0x1B5F1F8, Convert.FromHexString("41891F41C7470400000000")),
        (0x1B5F0C3, Convert.FromHexString("4585E47EE64863C848")),
        (0x1B2F629, Convert.FromHexString("BEC8000000E82D553E00")),
        (0x1B2F643, Convert.FromHexString("C5F25C4048C5E857D2")),
        (0x1F14BB1, Convert.FromHexString("83B87009000000"))
    ];
    private readonly HashSet<ulong> ownedSites = [];
    private ulong curveFile, curveRow;
    private byte[]? originalCurve;

    public void Apply(bool freeLevels, bool freeUpgrades)
    {
        session.ValidateImage(); session.ReadCharacter();
        if (!freeLevels && !freeUpgrades) { Restore(); return; }
        // Validate only the requested subsystem, before any mutations.
        foreach (var f in Fingerprints.Where((_, i) => i < 3 ? freeUpgrades : freeLevels))
            if (!memory.Read(session.ImageBase + f.Offset, f.Bytes.Length).SequenceEqual(f.Bytes))
                throw new IOException($"Progression cost instructions differ at image+0x{f.Offset:X}. No new cost changes were applied.");
        if (freeLevels == (originalCurve != null) && freeUpgrades == (ownedSites.Count == UpgradeSites.Length) &&
            (!freeLevels || GraphFile() == curveFile && memory.Read(curveRow + 0x3C, 16).SequenceEqual(new byte[16])) &&
            UpgradeSites.Where(x => ownedSites.Contains(x.Offset)).All(x => memory.Read(session.ImageBase + x.Offset, x.Free.Length).SequenceEqual(x.Free)))
            return; // Stable polling performs reads only; do not suspend every frame.
        memory.Suspended(() =>
        {
            session.ReadCharacter();
            if (freeLevels) EnableLevels(); else RestoreLevels();
            if (freeUpgrades)
            {
                foreach (var site in UpgradeSites)
                {
                    var expected = ownedSites.Contains(site.Offset) ? site.Free : site.Original;
                    if (!memory.Read(session.ImageBase + site.Offset, expected.Length).SequenceEqual(expected))
                        throw new IOException("Another patch changed weapon-upgrade costs. Its instructions were preserved.");
                }
                foreach (var site in UpgradeSites)
                    if (!ownedSites.Contains(site.Offset))
                    {
                        // Record ownership before writing so a partially failed patch is cleaned up.
                        ownedSites.Add(site.Offset);
                        memory.ReplaceProgressionCode(session.ImageBase, site.Offset, site.Original, site.Free);
                    }
            }
            else RestoreUpgrades();
        });
    }
    private ulong GraphFile()
    {
        var repo = memory.Pointer(session.ImageBase + 0x5540340);
        if (memory.Int32(repo + 0x970) != 1) throw new IOException("A unique level-cost parameter table is not loaded.");
        var cap = memory.Pointer(repo + 0x978);
        return memory.Pointer(memory.Pointer(cap + 0x70) + 0x70);
    }
    private void EnableLevels()
    {
        var file = GraphFile();
        if (originalCurve != null && curveFile == file)
        {
            if (!memory.Read(curveRow + 0x3C, 16).SequenceEqual(new byte[16]))
                throw new IOException("Another tool changed level-up costs. Its values were preserved.");
            return;
        }
        // An unloaded table is never written through cached pointers.
        originalCurve = null; curveFile = curveRow = 0;
        var header = memory.Read(file, 0x40);
        if (!Encoding.ASCII.GetString(header, 12, 21).StartsWith("CACL_CORRECT_GRAPH_ST", StringComparison.Ordinal))
            throw new IOException("Level-cost parameter type failed validation.");
        var count = BitConverter.ToUInt16(header, 10);
        if (count is < 1 or > 30000) throw new IOException("Invalid level-cost row count.");
        var wide = header[0x2D] > 3 && (header[0x2E] & 2) != 0;
        var start = header[0x2D] == 2 ? 0x34 : 0x40; var stride = wide ? 24 : 12;
        var records = memory.Read(file + (ulong)start, count * stride);
        var matches = new List<ulong>();
        for (var i = 0; i < count; i++)
        {
            if (BitConverter.ToInt32(records, i * stride) != 200) continue;
            var offset = wide ? BitConverter.ToUInt64(records, i * stride + 8) : BitConverter.ToUInt32(records, i * stride + 4);
            if (offset < (ulong)(start + count * stride) || offset > 0x2000000) throw new IOException("Invalid level-cost row offset.");
            matches.Add(file + offset);
        }
        if (matches.Count != 1) throw new IOException("No unique level-cost curve 200 found.");
        var row = matches[0]; var original = memory.Read(row + 0x3C, 16);
        if (Enumerable.Range(0, 4).Any(i => !float.IsFinite(BitConverter.ToSingle(original, i * 4))))
            throw new IOException("Invalid level-cost formula.");
        if (GraphFile() != file) throw new IOException("Level-cost table changed before applying.");
        curveFile = file; curveRow = row; originalCurve = original;
        memory.Write(row + 0x3C, new byte[16]);
        if (!memory.Read(row + 0x3C, 16).SequenceEqual(new byte[16])) throw new IOException("Level costs changed during the write.");
    }
    private void RestoreLevels()
    {
        if (originalCurve == null) return;
        if (GraphFile() != curveFile) { originalCurve = null; curveFile = curveRow = 0; return; }
        var current = memory.Read(curveRow + 0x3C, 16);
        if (!current.SequenceEqual(new byte[16]) && !current.SequenceEqual(originalCurve))
            throw new IOException("Level-cost values changed externally; they were not overwritten.");
        if (!current.SequenceEqual(originalCurve)) memory.Write(curveRow + 0x3C, originalCurve);
        originalCurve = null; curveFile = curveRow = 0;
    }
    private void RestoreUpgrades()
    {
        var changed = false;
        foreach (var site in UpgradeSites.Where(x => ownedSites.Contains(x.Offset)))
        {
            var current = memory.Read(session.ImageBase + site.Offset, site.Free.Length);
            if (current.SequenceEqual(site.Free)) memory.ReplaceProgressionCode(session.ImageBase, site.Offset, site.Free, site.Original);
            else if (!current.SequenceEqual(site.Original)) { changed = true; continue; }
            ownedSites.Remove(site.Offset);
        }
        if (changed) throw new IOException("Upgrade-cost instructions changed externally; they were not overwritten.");
    }
    public void Restore()
    {
        if (originalCurve == null && ownedSites.Count == 0) return;
        memory.Suspended(() =>
        {
            var errors = new List<string>();
            try { RestoreLevels(); } catch (IOException ex) { errors.Add(ex.Message); }
            try { RestoreUpgrades(); } catch (IOException ex) { errors.Add(ex.Message); }
            if (errors.Count != 0) throw new IOException(string.Join(" ", errors));
        });
    }
}
