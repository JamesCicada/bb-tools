using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BBTools;
using BBTools.Memory;

internal static class Program
{
    [DllImport("kernel32.dll")] private static extern nint VirtualAlloc(nint address, nuint size, uint type, uint protection);
    [DllImport("kernel32.dll")] private static extern bool VirtualFree(nint address, nuint size, uint type);
    [DllImport("kernel32.dll")] private static extern bool VirtualProtect(nint address, nuint size, uint protect, out uint old);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint Getter(nint actor);
    private static int checks;
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Contains("--fixture")) return Fixture();
        if (args.Contains("--render")) return Render(args[1], args.Length > 2 ? int.Parse(args[2]) : 1040, args.Length > 3 ? int.Parse(args[3]) : 860, args.Length > 4 ? args[4] : "Player");
        if (args.Contains("--verify-elf")) return VerifyElf(args.Last());
        try { Integration(); Console.WriteLine($"PASS: {checks} checks against a real synthetic Windows process."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++; Console.WriteLine($"PASS {message}");
    }
    private static void Reject<T>(Action operation, string message) where T : Exception
    {
        try { operation(); } catch (T) { checks++; Console.WriteLine($"PASS {message}"); return; }
        throw new Exception($"Expected rejection: {message}");
    }
    private static int Fixture()
    {
        var allocation = VirtualAlloc(0, 0x6000000, 0x3000, 4);
        if (allocation == 0) return 1;
        var root = (ulong)allocation;
        void Write(ulong address, byte[] bytes) => Marshal.Copy(bytes, 0, (nint)address, bytes.Length);
        void Ptr(ulong address, ulong value) => Write(address, BitConverter.GetBytes(value));
        void Int(ulong address, int value) => Write(address, BitConverter.GetBytes(value));
        try
        {
            foreach (var f in PlayerSession.Fingerprints.Concat(PlayerSession.FeatureFingerprints).Concat(GameActions.Fingerprints)) Write(root + f.Offset, f.Bytes);
            ulong world = root + 0x5700000, actor = root + 0x5710000, modules = root + 0x5720000, stats = root + 0x5730000, manager = root + 0x5740000, data = root + 0x5750000;
            Ptr(root + PlayerSession.WorldPointer, world); Ptr(world + 0x60, actor);
            Ptr(actor + 0x3B0, modules); Ptr(modules + 0x20, stats); Ptr(stats + 8, actor);
            Int(stats + 0xF8, 300); Int(stats + 0xFC, 600); Int(stats + 0x134, 70); Int(stats + 0x138, 100);
            Write(stats + 0x200, [0x05]);
            Ptr(root + 0x553B130, manager); Ptr(manager + 8, data); Ptr(actor + 0x3C0, data);
            foreach (var offset in new ulong[] {0x40,0x48,0x58,0x60,0x68,0x70}) Int(data + offset, 10);
            Int(data + 0x90, 10); Int(data + 0x94, 1200); Int(data + 0x44, 123456);
            var vtable = root + 0x5760000;
            for (ulong offset = 0; offset < 0x400; offset += 8) Ptr(vtable + offset, root + 0x18FD960);
            Ptr(actor, vtable);
            // Synthetic SysV jobs: record item/quantity, record an ordered sequence, or recursively call the getter.
            Write(root + 0x5780000, Convert.FromHexString("8B460489078B4608894704C3"));
            Write(root + 0x5780100, Convert.FromHexString("8B076BC00A01F08907C3"));
            Write(root + 0x5780200, Convert.FromHexString("534889FB4889D7488B07FF90C8010000488943105BC3"));
            // Windows ABI wrapper -> SysV getter, including Windows nonvolatile RSI/RDI.
            var wrapper = VirtualAlloc(0, 4096, 0x3000, 4);
            var bridge = Enumerable.Repeat((byte)0x90, 0x1C).ToArray();
            Convert.FromHexString("554889E5488B07").CopyTo(bridge, 0);
            Convert.FromHexString("FF90C80100005DC3").CopyTo(bridge, 0x14);
            Write(root + 0x18F7880, bridge);
            var wrapperBytes = Convert.FromHexString("57564883EC284889CF48B8")
                .Concat(BitConverter.GetBytes(root + 0x18F7880)).Concat(Convert.FromHexString("FFD04883C4285E5FC3")).ToArray();
            Marshal.Copy(wrapperBytes, 0, wrapper, wrapperBytes.Length);
            VirtualProtect(wrapper, 4096, 0x20, out _);
            VirtualProtect((nint)(root + 0x18FD000), 4096, 0x20, out _);
            VirtualProtect((nint)(root + 0x18F7000), 4096, 0x20, out _);
            VirtualProtect((nint)(root + 0x5780000), 4096, 0x20, out _);
            var getter = Marshal.GetDelegateForFunctionPointer<Getter>(wrapper);
            var otherWrapper = VirtualAlloc(0, 4096, 0x3000, 4);
            var otherBytes = Convert.FromHexString("57564883EC284889CF488B07FF90C80100004883C4285E5FC3");
            Marshal.Copy(otherBytes, 0, otherWrapper, otherBytes.Length); VirtualProtect(otherWrapper, 4096, 0x20, out _);
            var otherGetter = Marshal.GetDelegateForFunctionPointer<Getter>(otherWrapper);
            var running = true;
            var thread = new Thread(() =>
            {
                while (Volatile.Read(ref running))
                {
                    var mode = Marshal.ReadInt32((nint)(root + 0x5790024));
                    Marshal.WriteInt32((nint)(root + 0x5790028), mode);
                    if (mode == 1) otherGetter((nint)actor); else getter((nint)actor);
                    Thread.Sleep(5);
                }
            }) { IsBackground = true };
            thread.Start();
            Console.WriteLine(root); Console.Out.Flush();
            Console.ReadLine(); Volatile.Write(ref running, false); thread.Join(2000); return 0;
        }
        finally { VirtualFree(allocation, 0, 0x8000); }
    }
    private static void Integration()
    {
        using var child = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--fixture")
        {
            RedirectStandardOutput = true, RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true
        })!;
        try
        {
            var root = ulong.Parse(child.StandardOutput.ReadLine()!);
            using var memory = new ProcessMemory(child.Id);
            Check(memory.IsAlive, "open synthetic process");
            Check(PlayerSession.FindImage(memory, memory.AllocationBases()) == root, "discover relocated image through VirtualQueryEx");
            var session = new PlayerSession(memory, root);
            var player = session.ReadPlayer();
            Check(player.Health == 300 && player.MaxHealth == 600 && player.Stamina == 70, "resolve local player and resource fields");
            session.SetHealth(450); Check(session.ReadPlayer().Health == 450, "set health through WriteProcessMemory");
            Reject<ArgumentException>(() => session.SetHealth(601), "reject health beyond maximum");
            Reject<ArgumentException>(() => session.SetHealth(0), "reject zero health input");
            session.KeepHealthFull(); Check(session.ReadPlayer().Health == 600, "health refill");
            memory.SetInt32(player.Component + 0xF8, 0); session.KeepHealthFull();
            Check(session.ReadPlayer().Health == 0, "auto refill does not revive dead player");
            memory.SetInt32(player.Component + 0xF8, 600);
            session.SetInfiniteStamina(true); Check(memory.Read(player.Component + 0x200, 1)[0] == 0x85, "enable stamina flag without touching other bits");
            memory.Write(player.Component + 0x200, [0x87]); session.RestoreStamina();
            Check(memory.Read(player.Component + 0x200, 1)[0] == 7, "restore original stamina bit while preserving new unrelated bits");
            memory.Write(player.Component + 0x200, [0x85]); session.SetInfiniteStamina(true); session.RestoreStamina();
            Check(memory.Read(player.Component + 0x200, 1)[0] == 0x85, "preserve preexisting infinite stamina");
            var character = session.ReadCharacter();
            session.SetEchoes(987654); Check(session.ReadCharacter().Values.Echoes == 987654, "set echoes");
            Reject<ArgumentException>(() => session.SetEchoes(-1), "reject negative echoes");
            Reject<ArgumentException>(() => session.SetEchoes(1000000000), "reject excessive echoes");
            session.SetAttributes([11,12,13,14,15,16], character);
            var edited = session.ReadCharacter();
            Check(edited.Level == 31 && edited.Values.Arcane == 16, "apply six attributes and maintain level delta");
            Check(memory.Int32(character.Address + 0x44) == 123456 && edited.Values.Echoes == 987654, "attribute write preserves intervening fields and echoes");
            Reject<IOException>(() => session.SetAttributes([10,10,10,10,10,10], character), "reject stale stat draft");
            Reject<ArgumentException>(() => session.SetAttributes([100,10,10,10,10,10], edited), "reject stats above 99");
            session.SetPracticeFlag(PracticeFlag.GodMode, true);
            Check((memory.Read(player.Component + 0x200, 1)[0] & 3) == 3, "god mode sets no-damage and no-dead bits");
            session.SetPracticeFlag(PracticeFlag.OneShot, true);
            Check(memory.Read(root + 0x553E889, 1)[0] == 1, "one-shot uses player-exterminate flag");
            session.SetPracticeFlag(PracticeFlag.Stealth, true); session.SetPracticeFlag(PracticeFlag.Silence, true);
            session.RestorePracticeFlags();
            Check(memory.Read(player.Component + 0x200, 1)[0] == 0x85 && memory.Read(root + 0x553E889, 1)[0] == 0, "restore practice flags and preserve prior resource bits");
            var originalTable = memory.Pointer(player.Actor);
            using (var dispatcher = new GameThreadQueue(memory, session))
            {
                var payload = GameActions.ItemBatch(1000, 0x40000000, 20);
                memory.SetInt32(root + 0x5790024, 1);
                var acknowledged = Stopwatch.StartNew();
                while (memory.Int32(root + 0x5790028) != 1)
                {
                    if (acknowledged.Elapsed > TimeSpan.FromSeconds(2)) throw new IOException("Fixture did not acknowledge the alternate call site.");
                    Thread.Sleep(5);
                }
                var request = dispatcher.ExecuteAsync([new(root + 0x5780000, root + 0x5790000, dispatcher.Payload)], payload);
                Thread.Sleep(150);
                Check(memory.Int32(root + 0x5790000) == 0, "non-update getter callers cannot consume queued jobs");
                memory.SetInt32(root + 0x5790024, 0);
                request.GetAwaiter().GetResult();
                Check(memory.Int32(root + 0x5790000) == 0x400003E8 && memory.Int32(root + 0x5790004) == 20, "game-thread dispatch executes a real SysV job with item payload");
                dispatcher.ExecuteAsync([new(root + 0x5780100, root + 0x5790008, 1), new(root + 0x5780100, root + 0x5790008, 2), new(root + 0x5780100, root + 0x5790008, 3)]).GetAwaiter().GetResult();
                Check(memory.Int32(root + 0x5790008) == 123, "three-step native job preserves order");
                dispatcher.ExecuteAsync([new(root + 0x5780200, root + 0x5790000, 0, player.Actor)]).GetAwaiter().GetResult();
                Check(memory.Pointer(root + 0x5790010) == character.Address, "recursive getter preserves result without repeating the request");
            }
            Check(memory.Pointer(player.Actor) == originalTable, "dispatcher detaches and restores original actor vtable");
            Reject<ArgumentException>(() => GameActions.ItemBatch(1, 0, 20), "equipment quantity restricted to one");
            var newer = new ProcessCandidate(1, "Bloodborne", 200);
            var engine = new ProcessCandidate(2, "bb-probe", 100);
            Check(ProcessDiscovery.Best([new(newer, false), new(engine, true)]) == engine, "auto-connect prefers a validated loaded character");
            Check(ProcessDiscovery.Best([new(newer, true), new(engine, true)]) == engine, "auto-connect prefers the engine over a launcher when both validate");
            var settingsPath = Path.Combine(AppContext.BaseDirectory, $"bbtools-settings-{Guid.NewGuid():N}.json");
            try { new AppSettings(true).Save(settingsPath); Check(AppSettings.Load(settingsPath).AutoConnect, "auto-connect preference persists"); }
            finally { File.Delete(settingsPath); }
            memory.Write(root + PlayerSession.Fingerprints[0].Offset, [0]);
            Reject<IOException>(() => session.SetEchoes(1), "changed instruction fingerprint blocks writes");
            Check(session.ReadCharacter().Values.Echoes == 987654, "blocked operation leaves echoes unchanged");
            var first = PlayerSession.Fingerprints[0]; memory.Write(root + first.Offset, first.Bytes);
            memory.Write(player.Component + 8, BitConverter.GetBytes(player.Actor + 8));
            Reject<IOException>(() => session.SetHealth(500), "wrong player component owner blocks writes");
            memory.Write(player.Component + 8, BitConverter.GetBytes(player.Actor));
            var world = memory.Pointer(root + PlayerSession.WorldPointer);
            memory.Write(world + 0x60, new byte[8]);
            Reject<IOException>(() => session.SetHealth(500), "load screen null player blocks writes");
            child.StandardInput.WriteLine(); child.WaitForExit(5000);
            Check(!memory.IsAlive, "detect process exit");
            Reject<IOException>(() => memory.SetInt32(player.Component + 0xF8, 1), "reject writes after process exit");
        }
        finally { if (!child.HasExited) child.Kill(); }
    }
    private static int Render(string path, int width, int height, string page)
    {
        var app = new Application();
        var window = new MainWindow { Width = width, Height = height };
        var nav = (System.Windows.Controls.Button)window.FindName("Nav" + page);
        nav.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, height)); content.Arrange(new Rect(0,0,width,height)); content.UpdateLayout();
        var image = new RenderTargetBitmap(width, height, 96,96, PixelFormats.Pbgra32); image.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using (var stream = File.Create(path)) encoder.Save(stream);
        window.Close(); Console.WriteLine(path); return 0;
    }
    private static int VerifyElf(string path)
    {
        // Read-only comparison against a private prepared ELF; no game content is copied.
        var bytes = File.ReadAllBytes(path);
        if (!bytes.AsSpan(0, 4).SequenceEqual(new byte[] {0x7F, 0x45, 0x4C, 0x46})) throw new IOException("Not an ELF file.");
        var table = (int)BitConverter.ToUInt64(bytes, 32);
        var size = BitConverter.ToUInt16(bytes, 54);
        var count = BitConverter.ToUInt16(bytes, 56);
        foreach (var fingerprint in PlayerSession.Fingerprints.Concat(PlayerSession.FeatureFingerprints).Concat(GameActions.Fingerprints))
        {
            var matched = false;
            for (var i = 0; i < count; i++)
            {
                var header = table + size * i;
                if (BitConverter.ToUInt32(bytes, header) != 1) continue;
                var file = BitConverter.ToUInt64(bytes, header + 8);
                var address = BitConverter.ToUInt64(bytes, header + 16);
                var length = BitConverter.ToUInt64(bytes, header + 32);
                if (fingerprint.Offset < address || fingerprint.Offset + (ulong)fingerprint.Bytes.Length > address + length) continue;
                matched = bytes.AsSpan((int)(file + fingerprint.Offset - address), fingerprint.Bytes.Length).SequenceEqual(fingerprint.Bytes);
            }
            Check(matched, $"private image fingerprint at 0x{fingerprint.Offset:X}");
        }
        return 0;
    }
}
