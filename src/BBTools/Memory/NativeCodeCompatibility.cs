using System.IO;

namespace BBTools.Memory;

// Read-only recognition of the documented Nightwatch/visy observer ABI.
// No detour is removed, rewritten, skipped or cached. This verifies a wrapper
// shape, not the identity or correctness of the native callback's publisher.
public static class NativeCodeCompatibility
{
    public static Action<string>? Diagnostic { get; set; }
    private static readonly byte[] prefix = Convert.FromHexString(
        "488DA42480FFFFFF5051525657415041514152415341544989E44883E4F04881EC80000000")
        .Concat(Enumerable.Range(0,8).SelectMany(i => new byte[]{0xF3,0x0F,0x7F,(byte)(0x44|(i<<3)),0x24,(byte)(i*16)}))
        .Concat(new byte[]{0x48,0xB8}).ToArray();
    private static readonly byte[] suffix = new byte[]{0xFF,0xD0}
        .Concat(Enumerable.Range(0,8).SelectMany(i => new byte[]{0xF3,0x0F,0x6F,(byte)(0x44|(i<<3)),0x24,(byte)(i*16)}))
        .Concat(Convert.FromHexString("4C89E4415C415B415A415941585F5E5A5958488DA42480000000")).ToArray();

    public static bool Matches(IMemory memory, ulong image, ulong offset, byte[] expected)
    {
        var actual = memory.Read(image + offset, expected.Length);
        if (actual.SequenceEqual(expected)) return true;
        // Exact audited entry sites only. Replacement wrappers and other entry
        // points are deliberately excluded even if they begin with E9.
        if (offset is not (0x17D89F0 or 0x1BB7F60) || memory is not IExecutableMemory code || expected.Length < 13) return false;
        const int relocated = 13;
        if (actual[0] != 0xE9 || !actual.AsSpan(5, relocated-5).SequenceEqual(Enumerable.Repeat((byte)0x90, relocated-5).ToArray()) ||
            !actual.AsSpan(relocated).SequenceEqual(expected.AsSpan(relocated))) return false;
        try
        {
            var entry = checked(image + offset);
            var target = RelativeTarget(entry + 5, BitConverter.ToInt32(actual,1));
            var size = prefix.Length + 8 + suffix.Length + relocated + 5;
            if (target >= entry && target < entry+(ulong)expected.Length || !code.IsExecutable(entry, expected.Length) || !code.IsExecutable(target,size)) return false;
            var stub = memory.Read(target,size);
            if (!stub.AsSpan(0,prefix.Length).SequenceEqual(prefix) || !stub.AsSpan(prefix.Length+8,suffix.Length).SequenceEqual(suffix)) return false;
            var callback = BitConverter.ToUInt64(stub,prefix.Length);
            if (callback >= target && callback < target+(ulong)size || callback >= entry && callback < entry+(ulong)expected.Length || !code.IsExecutable(callback,1)) return false;
            var resume = prefix.Length+8+suffix.Length;
            if (!stub.AsSpan(resume,relocated).SequenceEqual(expected.AsSpan(0,relocated)) || stub[resume+relocated]!=0xE9 ||
                RelativeTarget(target+(ulong)size,BitConverter.ToInt32(stub,size-4)) != entry+relocated) return false;
            // Reject torn/changed code; publication callers repeat this check
            // with game threads suspended immediately before queuing the call.
            if (!memory.Read(entry,expected.Length).SequenceEqual(actual) || !memory.Read(target,size).SequenceEqual(stub)) return false;
            Diagnostic?.Invoke($"Accepted Nightwatch-compatible observer at image+0x{offset:X}; wrapper 0x{target:X}, callback 0x{callback:X}. Native entry and observer preserved.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or OverflowException) { return false; }
    }
    private static ulong RelativeTarget(ulong next, int displacement)
    {
        var result = checked((long)next+displacement);
        if (result < 0x10000 || result >= 0x0000800000000000) throw new IOException("Invalid compatibility branch target.");
        return (ulong)result;
    }
}
