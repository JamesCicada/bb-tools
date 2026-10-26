using System.IO;
using BBTools;
using BBTools.Memory;

internal static partial class Program
{
    private static void ProgressionChecks(ProcessMemory memory, PlayerSession session, ulong root)
    {
        var character = session.ReadCharacter();
        var originalInsight = memory.Read(character.Address + 0x84, 4);
        memory.Write(root + PlayerSession.InsightFingerprint.Offset, PlayerSession.InsightFingerprint.Bytes);
        try
        {
            session.SetInsight(99); Check(session.ReadInsight() == 99, "Insight setter reads back the supported maximum");
            session.SetInsight(0); Check(session.ReadInsight() == 0, "Insight setter accepts zero without changing Echoes or attributes");
            Reject<ArgumentException>(() => session.SetInsight(-1), "negative Insight is rejected");
            Reject<ArgumentException>(() => session.SetInsight(100), "Insight above 99 is rejected");
            memory.SetInt32(character.Address + 0x84, 100);
            Reject<IOException>(() => session.SetInsight(20), "invalid existing Insight layout blocks mutation");
            memory.SetInt32(character.Address + 0x84, 20);
            memory.Write(root + PlayerSession.InsightFingerprint.Offset, [0]);
            Reject<IOException>(() => session.SetInsight(30), "modified Insight reader instructions block editing");
            Check(memory.Int32(character.Address + 0x84) == 20 && session.ReadCharacter() == character,
                "rejected Insight edits preserve the character and unrelated resources");
        }
        finally
        {
            memory.Write(character.Address + 0x84, originalInsight);
            memory.Write(root + PlayerSession.InsightFingerprint.Offset, PlayerSession.InsightFingerprint.Bytes);
        }

        var repoSlot = root + 0x5540340; var previousRepo = memory.Read(repoSlot, 8);
        var repo = root + 0x5D00000; var cap = repo + 0x1000; var holder = repo + 0x2000;
        var file = repo + 0x10000; var row = file + 0x180;
        void Ptr(ulong at, ulong value) => memory.Write(at, BitConverter.GetBytes(value));
        var formula = new[] { .1f, 1f, .02f, 92f }.SelectMany(BitConverter.GetBytes).ToArray();
        void Table(ulong at)
        {
            var header = new byte[0x40]; System.Text.Encoding.ASCII.GetBytes("CACL_CORRECT_GRAPH_ST").CopyTo(header, 12);
            BitConverter.GetBytes((ushort)2).CopyTo(header, 10); header[0x2D] = 4; header[0x2E] = 2;
            memory.Write(at, header); memory.SetInt32(at + 0x40, 100); Ptr(at + 0x48, 0x100);
            memory.SetInt32(at + 0x58, 200); Ptr(at + 0x60, 0x180);
            memory.Write(at + 0x180 + 0x3C, formula);
        }
        foreach (var f in ProgressionCosts.Fingerprints) memory.Write(root + f.Offset, f.Bytes);
        foreach (var s in ProgressionCosts.UpgradeSites) memory.Write(root + s.Offset, s.Original);
        var costs = new ProgressionCosts(memory, session);
        try
        {
            Ptr(repoSlot, repo); memory.SetInt32(repo + 0x970, 1); Ptr(repo + 0x978, cap);
            Ptr(cap + 0x70, holder); Ptr(holder + 0x70, file); Table(file);
            var originalStats = session.ReadCharacter();
            costs.Apply(true, true);
            Check(memory.Read(row + 0x3C, 16).All(b => b == 0) && ProgressionCosts.UpgradeSites.All(s => memory.Read(root + s.Offset, s.Free.Length).SequenceEqual(s.Free)),
                "free progression clears only level-cost formula and native upgrade Echoes/material costs");
            costs.Apply(true, true);
            Check(session.ReadCharacter() == originalStats, "stable cost polling does not edit character, inventory or Echoes");
            costs.Apply(false, true);
            Check(memory.Read(row + 0x3C, 16).SequenceEqual(formula) && memory.Read(root + ProgressionCosts.UpgradeSites[0].Offset, 3).SequenceEqual(ProgressionCosts.UpgradeSites[0].Free),
                "level-up and weapon-upgrade toggles restore independently");
            costs.Restore();
            Check(ProgressionCosts.UpgradeSites.All(s => memory.Read(root + s.Offset, s.Original.Length).SequenceEqual(s.Original)), "normal upgrade costs restore on cleanup");
            costs.Apply(true, false);
            memory.Write(row + 0x3C, BitConverter.GetBytes(2f));
            Reject<IOException>(costs.Restore, "external level-cost edits are preserved during cleanup");
            Check(BitConverter.ToSingle(memory.Read(row + 0x3C, 4)) == 2, "foreign level-cost value is not overwritten");
            memory.Write(row + 0x3C, new byte[16]); costs.Restore();
            costs.Apply(true, false);
            var replacement = file + 0x1000; Table(replacement); Ptr(holder + 0x70, replacement);
            costs.Apply(true, false);
            Check(memory.Read(replacement + 0x180 + 0x3C, 16).All(b => b == 0), "free levels resume against a newly loaded parameter table");
            memory.Write(row + 0x3C, BitConverter.GetBytes(7f)); costs.Restore();
            Check(BitConverter.ToSingle(memory.Read(row + 0x3C, 4)) == 7 && memory.Read(replacement + 0x180 + 0x3C, 16).SequenceEqual(formula),
                "cleanup restores the current table and never writes stale unloaded rows");
            costs.Apply(false, true);
            var site = ProgressionCosts.UpgradeSites[0]; memory.Write(root + site.Offset, [0xCC, 0x90, 0x90]);
            Reject<IOException>(costs.Restore, "external upgrade-code changes report a conflict");
            Check(memory.Read(root + site.Offset, 3)[0] == 0xCC && memory.Read(root + ProgressionCosts.UpgradeSites[1].Offset, 5).SequenceEqual(ProgressionCosts.UpgradeSites[1].Original),
                "cleanup preserves foreign code and still restores the independently owned material site");
            memory.Write(root + site.Offset, site.Original); costs.Restore();
            memory.Write(root + ProgressionCosts.Fingerprints[0].Offset, [0]);
            Reject<IOException>(() => costs.Apply(false, true), "unsupported upgrade instructions fail before applying patches");
            Check(memory.Read(root + site.Offset, 3).SequenceEqual(site.Original), "profile rejection leaves upgrade costs untouched");
            var preset = new AdvancedSettings(FreeLevels: true, FreeUpgrades: true);
            var roundTrip = System.Text.Json.JsonSerializer.Deserialize<AdvancedSettings>(System.Text.Json.JsonSerializer.Serialize(preset));
            Check(roundTrip == preset && !System.Text.Json.JsonSerializer.Deserialize<AdvancedSettings>("{}")!.FreeLevels,
                "progression settings round-trip through presets and older presets default off");
        }
        finally
        {
            costs.Restore(); memory.Write(repoSlot, previousRepo);
            foreach (var f in ProgressionCosts.Fingerprints) memory.Write(root + f.Offset, f.Bytes);
        }
    }
}
