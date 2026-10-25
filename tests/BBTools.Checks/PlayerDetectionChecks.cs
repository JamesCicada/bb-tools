using System.ComponentModel;
using System.IO;
using BBTools;
using BBTools.Memory;

internal static partial class Program
{
    private sealed class DeniedMemory : IMemory
    {
        public byte[] Read(ulong address,int size) => throw new IOException("Read denied",new Win32Exception(5));
        public void Write(ulong address,byte[] value) => throw new Exception("Unexpected write");
    }
    private sealed class TransientOwnerMemory(IMemory source, ulong ownerAddress) : IMemory
    {
        public int OwnerReads;
        public byte[] Read(ulong address, int size)
        {
            var value = source.Read(address, size);
            if (address == ownerAddress && size == 8 && OwnerReads++ == 0)
                return BitConverter.GetBytes(BitConverter.ToUInt64(value) + 8);
            return value;
        }
        public void Write(ulong address, byte[] value) => throw new Exception("Snapshot retries must be read-only.");
    }
    private static void PlayerDetectionChecks(ProcessMemory memory, PlayerSession session, ulong root)
    {
        var player = session.ReadPlayer();
        var before = memory.Int32(player.Component + 0x134);
        memory.SetInt32(player.Component + 0x134, -9);
        Check(session.ReadPlayer().Actor == player.Actor && session.ReadPlayer().Stamina == 0 && memory.Int32(player.Component + 0x134) == -9,
            "normal negative stamina keeps the player valid, displays zero and performs no corrective writes");
        Check(session.ReadCharacter().Address != 0, "character reads remain available during stamina overdraft");
        memory.SetInt32(player.Component + 0x134, int.MinValue);
        Reject<IOException>(() => session.ReadPlayer(), "malformed stamina still fails player validation");
        memory.SetInt32(player.Component + 0x134, before);
        var race = new TransientOwnerMemory(memory, player.Component + 8);
        Check(new PlayerSession(race,root).ReadPlayer().Actor == player.Actor && race.OwnerReads >= 3,
            "read-only snapshot retries recover a transient owner mismatch and verify the final owner");
        memory.Write(player.Component + 8, BitConverter.GetBytes(player.Actor + 8));
        Reject<IOException>(() => session.ReadPlayer(), "persistent resource owner mismatches remain blocked");
        memory.Write(player.Component + 8, BitConverter.GetBytes(player.Actor));
        Check(ProcessAccessErrors.IsAccessDenied(new Win32Exception(5)) && ProcessAccessErrors.IsAccessDenied(new IOException("read failed",new Win32Exception(5))),
            "connection permission detection preserves native access-denied errors through wrappers");
        Check(!ProcessAccessErrors.IsAccessDenied(new Win32Exception(299)) && !ProcessAccessErrors.IsAccessDenied(new IOException("Player loading")),
            "loading and partial-copy errors never trigger an administrator suggestion");
        Reject<Win32Exception>(()=>ProcessDiscovery.FindBest([new(123,"bb-probe",0,5)],()=>false),
            "automatic connection surfaces confirmed restricted game process access instead of silently finding nothing");
        try { PlayerSession.FindImage(new DeniedMemory(),[root]); throw new Exception("Expected denied image read"); }
        catch(IOException error) { Check(ProcessAccessErrors.IsAccessDenied(error),"image discovery preserves access denial instead of reporting an unsupported game image"); }
        var message = typeof(MainWindow).GetMethod("ConnectionFailureMessage",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
        Check(((string)message.Invoke(null,[new Win32Exception(5)])!).Contains("administrator") &&
            (string)message.Invoke(null,[new IOException("Player loading")])! == "Player loading",
            "connection UI suggests administrator only for confirmed permission errors");
    }
}
