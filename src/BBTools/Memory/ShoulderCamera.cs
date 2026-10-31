using System.IO;

namespace BBTools.Memory;

// Shift the freshly composed normal camera along its own right vector. Replaying
// the native stores each frame prevents accumulation; no parameter X field exists.
public sealed class ShoulderCamera(IGameMemory memory, PlayerSession session)
{
    public const ulong Offset = 0x193BEAD;
    public static readonly byte[] Original = Convert.FromHexString("C4C178295730C4C178295F40498BBD10010000");
    private ulong controls, code;
    private byte[]? installed;
    public void Apply(float metres)
    {
        if (!float.IsFinite(metres) || metres is < -2 or > 2) throw new ArgumentException("Horizontal camera offset must be between -2 and 2 metres.");
        if (metres == 0) { Restore(); return; }
        session.ValidateImage(); var player = session.ReadPlayer();
        if (player.Health == 0) { Pause(); return; }
        var expected = installed ?? Original;
        if (!memory.Read(session.ImageBase + Offset, expected.Length).SequenceEqual(expected))
            throw new IOException("Horizontal camera instructions changed externally; no patch was applied.");
        if (installed != null && memory.Read(controls, 4).SequenceEqual(BitConverter.GetBytes(metres)) &&
            BitConverter.ToUInt64(memory.Read(controls + 8, 8)) == player.Actor) return;
        memory.Suspended(() =>
        {
            var current = session.ReadPlayer();
            if (controls == 0) { controls = memory.AllocateData(16); code = memory.AllocateCode(Build(session.ImageBase, controls)); }
            if (installed == null)
            {
                installed = Convert.FromHexString("FF2500000000").Concat(BitConverter.GetBytes(code)).Concat(new byte[Original.Length - 14].Select(_ => (byte)0x90)).ToArray();
                memory.ReplaceShoulderCode(session.ImageBase, Original, installed);
            }
            memory.Write(controls + 8, BitConverter.GetBytes(current.Actor));
            memory.Write(controls, BitConverter.GetBytes(metres));
        });
    }
    public void Pause() { if (controls != 0) memory.Write(controls, new byte[4]); }
    public void Restore()
    {
        if (installed == null) return;
        memory.Suspended(() =>
        {
            Pause();
            var current = memory.Read(session.ImageBase + Offset, Original.Length);
            if (current.SequenceEqual(installed)) memory.ReplaceShoulderCode(session.ImageBase, installed, Original);
            else if (!current.SequenceEqual(Original)) throw new IOException("Another tool changed the camera hook; its instructions were preserved. This tool's offset is disabled.");
            installed = null;
        });
    }
    internal static byte[] Build(ulong image, ulong controls)
    {
        using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
        var exits = new List<long>();
        void B(string hex) => w.Write(Convert.FromHexString(hex));
        void Skip(string op) { B(op); exits.Add(stream.Position); w.Write(0); }
        // Native forward/position stores and debug-controller load (no relocated RIP operands).
        w.Write(Original); B("9C50514883EC20F30F7F0424F30F7F4C2410");
        B("48B8"); w.Write(controls); B("833800"); Skip("0F84");
        B("48B9"); w.Write(image + PlayerSession.WorldPointer); B("488B094885C9"); Skip("0F84");
        B("488B496048394808"); Skip("0F85");
        B("48B9"); w.Write(image + 0x553E860); B("488B094885C9"); Skip("0F84");
        B("488B893028000049398D28010000"); Skip("0F85");
        B("4885FF"); Skip("0F84"); B("83BFC400000000"); Skip("0F85");
        // Legacy SSE preserves upper YMM lanes; both lower scratch registers and flags restore.
        // Store XYZ only so an unusual basis W can never change position W.
        B("F30F10080FC6C900410F1047100F59C1410F584740410F1347400F15C0F3410F114748");
        var done = stream.Position;
        B("F30F6F0424F30F6F4C24104883C42059589DFF2500000000"); w.Write(image + Offset + (ulong)Original.Length);
        var end = stream.Position; foreach (var at in exits) { stream.Position = at; w.Write((int)(done - at - 4)); }
        stream.Position = end; return stream.ToArray();
    }
}
