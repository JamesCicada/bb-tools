using System.IO;

namespace BBTools.Memory;

public sealed record NativeCall(ulong Function, params ulong[] Arguments);

// Intercept only this player's data getter through a private vtable copy. Game code stays unchanged.
public sealed class GameThreadQueue : IDisposable
{
    private readonly ProcessMemory memory;
    private readonly PlayerSession session;
    private readonly ulong actor, data, originalTable, table, workspace;
    private readonly CancellationTokenSource stopped = new();
    private bool busy, disposed;
    private int lastCallCount;
    public const int PayloadOffset = 512;
    public ulong Payload => workspace + PayloadOffset;
    public ulong Actor => actor;
    public bool MatchesCurrentPlayer() => !disposed && session.ReadPlayer().Actor == actor &&
        session.ReadCharacter().Address == data && memory.Pointer(actor) == table;

    public GameThreadQueue(ProcessMemory memory, PlayerSession session)
    {
        this.memory = memory; this.session = session;
        session.ValidateImage();
        actor = session.ReadPlayer().Actor;
        data = session.ReadCharacter().Address;
        originalTable = memory.Pointer(actor);
        var getter = memory.Pointer(originalTable + 0x1C8);
        if (getter != session.ImageBase + 0x18FD960 || !memory.Read(getter, 8).SequenceEqual(Convert.FromHexString("488B87C0030000C3")))
            throw new IOException("The player's data getter is modified or unsupported. Item/warp requests were not installed.");
        var length = 0;
        while (length < 0x2000)
        {
            var pointer = BitConverter.ToUInt64(memory.Read(originalTable + (ulong)length, 8));
            if (pointer < session.ImageBase || pointer >= session.ImageBase + 0x3000000) break;
            length += 8;
        }
        if (length < 0x1D0 || length == 0x2000) throw new IOException("The player vtable boundary could not be verified.");
        var original = memory.Read(originalTable - 16, length + 16);
        workspace = memory.AllocateData(16384);
        table = workspace + 1040; // Keep the Itanium RTTI prefix before the copied table.
        var code = memory.AllocateCode(BuildDispatcher(getter, workspace, session.ImageBase + PlayerSession.WorldPointer, session.ImageBase + 0x18F789A));
        BitConverter.GetBytes(code).CopyTo(original, 16 + 0x1C8);
        memory.Write(table - 16, original);
        memory.Write(workspace + 8, BitConverter.GetBytes(actor));
        memory.Write(workspace + 16, BitConverter.GetBytes(data));
        memory.Suspended(() =>
        {
            if (session.ReadPlayer().Actor != actor || memory.Pointer(actor) != originalTable || session.ReadCharacter().Address != data)
                throw new IOException("Player changed before installing the request dispatcher.");
            memory.Write(actor, BitConverter.GetBytes(table));
        });
    }
    public async Task ExecuteAsync(NativeCall[] calls, byte[]? payload = null)
    {
        if (disposed || busy) throw new IOException("A game request is already active or the dispatcher is closed.");
        if (calls.Length is < 1 or > 3 || calls.Any(x => x.Arguments.Length > 6) || payload?.Length > 512)
            throw new ArgumentException("Invalid game request.");
        busy = true;
        try
        {
            session.ValidateImage();
            memory.Suspended(() =>
            {
                if (session.ReadPlayer().Actor != actor || session.ReadCharacter().Address != data || memory.Pointer(actor) != table)
                    throw new IOException("Player changed. Reconnect before submitting a game request.");
                if (memory.Int32(workspace) is 1 or 2) throw new IOException("The previous game request has not finished. It will not be repeated.");
                var descriptors = new byte[3 * 64];
                lastCallCount = calls.Length;
                for (var i = 0; i < calls.Length; i++)
                {
                    BitConverter.GetBytes(calls[i].Function).CopyTo(descriptors, i * 64);
                    for (var j = 0; j < calls[i].Arguments.Length; j++) BitConverter.GetBytes(calls[i].Arguments[j]).CopyTo(descriptors, i * 64 + 8 + j * 8);
                }
                memory.Write(workspace + 64, descriptors);
                if (payload != null) memory.Write(Payload, payload);
                memory.SetInt32(workspace, 1); // Publish after all descriptors, while target threads are paused.
            });
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(50, stopped.Token);
                var state = memory.Int32(workspace);
                if (state == 3) return;
                if (!memory.IsAlive) throw new IOException("The game exited during the request.");
            }
            memory.Suspended(() => { if (memory.Int32(workspace) == 1) memory.SetInt32(workspace, 0); });
            throw new IOException("Game request timed out. It was not automatically repeated; check the game before retrying.");
        }
        finally { busy = false; }
    }
    public ulong Result(int index)
    {
        if (disposed || busy || index < 0 || index >= lastCallCount || memory.Int32(workspace) != 3)
            throw new IOException("The native request has no completed result at that index.");
        return BitConverter.ToUInt64(memory.Read(workspace + 64 + (ulong)index * 64 + 56, 8));
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; stopped.Cancel();
        if (!memory.IsAlive) return;
        memory.Suspended(() =>
        {
            if (memory.Int32(workspace) != 2) memory.SetInt32(workspace, 0);
            try
            {
                if (memory.Pointer(actor) == table && memory.Pointer(actor + 0x3C0) == data)
                    memory.Write(actor, BitConverter.GetBytes(originalTable));
            }
            catch (IOException) { /* An unloaded actor must not be written through a stale pointer. */ }
        });
        // Keep dispatcher pages until process exit: a suspended thread may have cached a vtable slot
        // or be returning from a native call. Freeing them on detach would create a use-after-free.
    }
    public static byte[] BuildDispatcher(ulong getter, ulong workspace, ulong worldSlot, ulong updateReturn)
    {
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        void Bytes(string hex) => writer.Write(Convert.FromHexString(hex));
        void Mov(string opcode, ulong value) { Bytes(opcode); writer.Write(value); }
        var exits = new List<long>();
        void ExitIf(string opcode) { Bytes(opcode); exits.Add(stream.Position); writer.Write(0); }
        // System V ABI: preserve all callee-saved registers, align the stack, retain the original result.
        Bytes("554889E55341544155415641574883EC084989FD");
        Mov("48B8", getter); Bytes("FFD04989C4");
        // Only the PlayerIns synchronization call site may consume jobs, not HUD/other getter callers.
        Mov("48B8", updateReturn); Bytes("48394508"); ExitIf("0F85");
        Mov("48BB", workspace);
        Bytes("4C3B6B08"); ExitIf("0F85");
        Bytes("4C3B6310"); ExitIf("0F85");
        Mov("48B8", worldSlot); Bytes("488B004885C0"); ExitIf("0F84");
        Bytes("4C396860"); ExitIf("0F85");
        Bytes("B801000000BA02000000F00FB113"); ExitIf("0F85"); // claim ready -> running once
        for (var i = 0; i < 3; i++)
        {
            var offset = 64 + i * 64;
            Bytes("488B83"); writer.Write(offset); Bytes("4885C00F84"); var skip = stream.Position; writer.Write(0);
            string[] args = ["488BBB", "488BB3", "488B93", "488B8B", "4C8B83", "4C8B8B"];
            for (var j = 0; j < args.Length; j++) { Bytes(args[j]); writer.Write(offset + 8 + j * 8); }
            Bytes("FFD0488983"); writer.Write(offset + 56);
            var next = stream.Position; stream.Position = skip; writer.Write((int)(next - skip - 4)); stream.Position = next;
        }
        Bytes("C70303000000");
        var end = stream.Position;
        Bytes("4C89E04883C408415F415E415D415C5B5DC3");
        var length = stream.Position;
        foreach (var fixup in exits) { stream.Position = fixup; writer.Write((int)(end - fixup - 4)); }
        stream.Position = length;
        return stream.ToArray();
    }
}
