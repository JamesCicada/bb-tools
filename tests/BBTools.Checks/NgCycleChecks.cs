using System.IO;
using BBTools.Memory;

internal static partial class Program
{
    private sealed class ChangingNgCounterMemory(IMemory source, ulong counter) : IMemory
    {
        private int reads;
        public byte[] Read(ulong address, int size)
        {
            var value = source.Read(address, size);
            return address == counter && size == 4 && ++reads == 2 ? BitConverter.GetBytes(BitConverter.ToInt32(value) + 1) : value;
        }
        public void Write(ulong address, byte[] value) => throw new Exception("NG snapshot must not write.");
    }
    private static void NgCycleChecks(ProcessMemory memory, PlayerSession session, ulong root)
    {
        var managerSlot = root + 0x553B130;
        var manager = memory.Pointer(managerSlot); var character = session.ReadCharacter();
        var original = memory.Read(manager + 0x60, 0x30);
        foreach (var f in PlayerSession.NgCycleFingerprints) memory.Write(root + f.Offset, f.Bytes);
        try
        {
            memory.SetInt32(manager + 0x68, 0); var initial = session.ReadNgCycle();
            session.SetNgCycle(6, initial);
            Check(session.ReadNgCycle().Value == 6, "NG editor sets the native clear counter and verifies its value");
            session.SetNgCycle(999, session.ReadNgCycle());
            Check(session.ReadNgCycle().Value == 999, "NG counter supports higher playthrough counts independently of the difficulty cap");
            session.SetNgCycle(0, session.ReadNgCycle());
            Check(session.ReadNgCycle().Value == 0 && session.ReadCharacter() == character, "NG editor returns to NG without changing attributes, level or Echoes");
            var after = memory.Read(manager + 0x60, 0x30); var expected = original.ToArray(); BitConverter.GetBytes(0).CopyTo(expected, 8);
            Check(after.SequenceEqual(expected), "NG writes preserve neighboring manager fields, ending state and death counters");
            Reject<ArgumentException>(() => session.SetNgCycle(-1, initial), "negative NG cycles are rejected");
            Reject<ArgumentException>(() => session.SetNgCycle(1000, initial), "out-of-range NG cycles are rejected");
            memory.SetInt32(manager + 0x68, 2);
            Reject<IOException>(() => session.SetNgCycle(4, initial), "an externally changed cycle requires refreshing the editor before writing");
            Check(memory.Int32(manager + 0x68) == 2, "stale NG edits leave the new counter intact");
            var captured = session.ReadNgCycle(); var replacement = memory.AllocateData(0x100);
            memory.Write(replacement + 8, BitConverter.GetBytes(character.Address)); memory.Write(managerSlot, BitConverter.GetBytes(replacement));
            Reject<IOException>(() => session.SetNgCycle(4, captured), "manager replacement blocks an old NG edit even when the character record address matches");
            Check(memory.Int32(replacement + 0x68) == 0 && memory.Int32(manager + 0x68) == 2, "rejected NG edits preserve both retired and replacement managers");
            memory.Write(managerSlot, BitConverter.GetBytes(manager));
            var changing = new PlayerSession(new ChangingNgCounterMemory(memory, manager + 0x68), root);
            Reject<IOException>(() => changing.ReadNgCycle(), "NG snapshot detects an in-flight counter change without performing a write");
            var fingerprint = PlayerSession.NgCycleFingerprints[0]; memory.Write(root + fingerprint.Offset, [0]);
            Reject<IOException>(() => session.SetNgCycle(3, captured), "modified NG getter instructions block mutation");
            Check(memory.Int32(manager + 0x68) == 2, "unsupported NG instructions leave the counter unchanged");
            memory.Write(root + fingerprint.Offset, fingerprint.Bytes);
            memory.SetInt32(manager + 0x68, -1);
            Reject<IOException>(() => session.ReadNgCycle(), "malformed native NG counters are rejected");
        }
        finally
        {
            memory.Write(managerSlot, BitConverter.GetBytes(manager)); memory.Write(manager + 0x60, original);
            foreach (var f in PlayerSession.NgCycleFingerprints) memory.Write(root + f.Offset, f.Bytes);
        }
    }
}
