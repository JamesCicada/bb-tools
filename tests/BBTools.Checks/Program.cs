using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Http;
using System.Text.Json;
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
        if (args.Contains("--render")) return Render(args[1], args.Length > 2 ? int.Parse(args[2]) : 1040, args.Length > 3 ? int.Parse(args[3]) : 860, args.Length > 4 ? args[4] : "Player", args.Length > 5 ? double.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture) : 0);
        if (args.Contains("--verify-elf")) return VerifyElf(args.Last());
        if (args.Contains("--check-updates"))
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var result = UpdateChecker.CheckAsync(client, AppInfo.Version).GetAwaiter().GetResult();
            Console.WriteLine($"Current={AppInfo.DisplayVersion}; Latest={result.LatestVersion}; UpdateAvailable={result.IsNewerThan(AppInfo.Version)}; Release={result.ReleaseUrl}");
            return 0;
        }
        try { UpdateChecks(); Integration(); Console.WriteLine($"PASS: {checks} checks (update service and real synthetic Windows process)."); return 0; }
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
    private sealed class FakeReleaseHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => respond(request, token);
    }
    private static void UpdateChecks()
    {
        const string release = "{\"tag_name\":\"v0.10.0\",\"draft\":false,\"prerelease\":false,\"html_url\":\"https://example.com/untrusted.exe\"}";
        using var client = new HttpClient(new FakeReleaseHandler((request, token) =>
        {
            Check(request.RequestUri!.AbsoluteUri == UpdateChecker.LatestReleaseApi && request.Headers.UserAgent.ToString() == "BBTools/0.9.0" && request.Headers.Authorization == null,
                "manual update check uses the fixed public GitHub endpoint and identifies the app without credentials");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(release) });
        }));
        var result = UpdateChecker.CheckAsync(client, new Version(0,9,0)).GetAwaiter().GetResult();
        Check(result.IsNewerThan(new Version(0,9,0)) && !result.IsNewerThan(new Version(0,10,0)) && !result.IsNewerThan(new Version(1,0,0)),
            "updates compare numeric versions and do not offer a downgrade or equal version");
        Check(result.ReleaseUrl.AbsoluteUri == AppInfo.RepositoryUrl + "/releases/tag/v0.10.0", "update link stays on the project release page despite a response-supplied external URL");
        UpdateResult Parse(string json) { using var doc = JsonDocument.Parse(json); return UpdateChecker.ParseRelease(doc.RootElement); }
        Reject<IOException>(() => Parse(release.Replace("v0.10.0", "v0.10.0-beta.1")), "update parser rejects prerelease-style or unsupported version tags");
        Reject<IOException>(() => Parse(release.Replace("\"draft\":false", "\"draft\":true")), "update parser rejects draft releases");
        Reject<IOException>(() => Parse(release.Replace("\"prerelease\":false", "\"prerelease\":true")), "update parser rejects prereleases");
        Reject<IOException>(() => Parse("{}"), "missing release fields cannot report that the app is up to date");
        foreach (var status in new[] { HttpStatusCode.NotFound, HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests })
        {
            using var failed = new HttpClient(new FakeReleaseHandler((_, _) => Task.FromResult(new HttpResponseMessage(status))));
            Reject<IOException>(() => UpdateChecker.CheckAsync(failed, new Version(0,1,1)).GetAwaiter().GetResult(), $"GitHub {status} reports an update-check failure");
        }
        using var cancelled = new HttpClient(new FakeReleaseHandler((_, _) => Task.FromCanceled<HttpResponseMessage>(new CancellationToken(true))));
        Reject<OperationCanceledException>(() => UpdateChecker.CheckAsync(cancelled, new Version(0,1,1)).GetAwaiter().GetResult(), "cancelled update checks do not become successful results");
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
            var transform = root + 0x57B0000;
            Ptr(modules + 0x68, transform); Int(actor + 0x3F8, 0x18010000);
            Write(transform + 0x1D4, BitConverter.GetBytes(1.5f));
            Write(transform + 0x1E0, BitConverter.GetBytes(10f).Concat(BitConverter.GetBytes(-20f)).Concat(BitConverter.GetBytes(30f)).ToArray());
            Int(stats + 0xF8, 300); Int(stats + 0xFC, 600); Int(stats + 0x134, 70); Int(stats + 0x138, 100);
            Write(stats + 0x200, [0x05]);
            Ptr(root + 0x553B130, manager); Ptr(manager + 8, data); Ptr(actor + 0x3C0, data);
            foreach (var offset in new ulong[] {0x40,0x48,0x58,0x60,0x68,0x70}) Int(data + offset, 10);
            Int(data + 0x90, 10); Int(data + 0x94, 1200); Int(data + 0x44, 123456);
            var vtable = root + 0x5760000;
            for (ulong offset = 0; offset < 0x400; offset += 8) Ptr(vtable + offset, root + 0x18FD960);
            Ptr(actor, vtable);
            // Synthetic SysV jobs: record item/quantity, record an ordered sequence, or recursively call the getter.
            Write(root + 0x5780000, Convert.FromHexString("8B460889078B460C8947048B4604894708C3"));
            Write(root + 0x5780100, Convert.FromHexString("8B076BC00A01F08907C3"));
            Write(root + 0x5780200, Convert.FromHexString("534889FB4889D7488B07FF90C8010000488943105BC3"));
            // Model a goods handle constructor and a temporary-handle release.
            Write(root + 0x5780300, Convert.FromHexString("8B0281E0FFFFFF0081C8000000B08907C3"));
            Write(root + 0x5780400, Convert.FromHexString("C70700000000C3"));
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
            session.SetPracticeFlag(PracticeFlag.NoAi, true); session.SetPracticeFlag(PracticeFlag.NoFallDamage, true);
            Check(memory.Read(root + 0x553E895, 1)[0] == 1 && memory.Read(player.Component + 0x200, 1)[0] == 0x8D, "disable AI and fall damage without altering resource flags");
            session.RestorePracticeFlags();
            Check(memory.Read(root + 0x553E895, 1)[0] == 0 && memory.Read(player.Component + 0x200, 1)[0] == 0x85, "restore AI and fall damage original bits");
            var spot = session.ReadLocation("Practice spot");
            Check(spot == new SavedLocation("Practice spot", 0x18010000, 10, -20, 30, 1.5f), "capture loaded map, position and heading from the player transform");
            var spotPayload = spot.Payload();
            Check(spotPayload.Length == 48 && BitConverter.ToUInt32(spotPayload) == spot.Block && BitConverter.ToSingle(spotPayload,16) == 10 && BitConverter.ToSingle(spotPayload,36) == 1.5f,
                "location relocation payload aligns block, position and yaw vectors for SysV pointer arguments");
            Reject<ArgumentException>(() => (spot with { X = float.NaN }).Payload(), "reject nonfinite saved coordinates");
            using (var actions = new GameActions(memory, session))
                Reject<IOException>(() => actions.RestoreLocationAsync(spot with { Block = 0x22000000 }), "reject restoring coordinates into an unloaded map before installing a dispatcher");
            Check(GameActions.Lamps.Length == 45 && GameActions.Lamps.Select(x => x.Entity).Distinct().Count() == 45 && GameActions.Lamps.Count(x => x.Name.StartsWith("[DLC]")) == 10,
                "lamp catalog contains 45 unique named destinations including all ten DLC lamps");
            Check(GameActions.Lamps.Single(x => x.Name == "Moonside Lake").Entity == 3202952 && GameActions.Lamps.Single(x => x.Name == "Lecture Building").Entity == 3202951,
                "Moonside Lake and Lecture Building use distinct verified return points");
            Check(BossDestinations.All.Length == 19 && BossDestinations.All.Count(x => x.Name.StartsWith("[DLC]")) == 5 && BossDestinations.All.All(x => GameActions.Lamps.Any(l => l.Entity == x.EntryLamp)),
                "nineteen boss entrances include all five DLC bosses and valid area lamps");
            foreach (var destination in BossDestinations.All) destination.Position.Validate();
            Check(ItemInput.Parse("400003E8", 0, true) == (1000, 0x40000000u) && ItemInput.Parse("0x400003E8", 0, false) == (1000, 0x40000000u), "parse Wiki hexadecimal IDs with category included");
            Check(ItemInput.Parse("1000", 0x40000000, false) == (1000, 0x40000000u) && ItemInput.Parse("40000320", 0, true) == (800,0x40000000u), "decimal and digits-only hexadecimal item IDs remain unambiguous");
            Reject<ArgumentException>(() => ItemInput.Parse("300003E8",0,true), "reject unknown item categories");
            var locationsPath = Path.Combine(AppContext.BaseDirectory, $"bbtools-locations-{Guid.NewGuid():N}.json");
            try { new LocationBook([spot]).Save(locationsPath); Check(LocationBook.Load(locationsPath).Locations.SequenceEqual(new[] { spot }), "named locations persist with map, coordinates and heading"); }
            finally { File.Delete(locationsPath); }
            var originalTable = memory.Pointer(player.Actor);
            using (var dispatcher = new GameThreadQueue(memory, session))
            {
                var payload = GameActions.ItemBatch(1000, 0x40000000, 20);
                Check(payload.SequenceEqual(Convert.FromHexString("0100000000000000E803004014000000FFFFFFFF")), "goods payload reserves a handle for engine construction and keeps catalog ID and quantity at engine offsets");
                Check(GameActions.ItemBatch(26000000, 0, 1).SequenceEqual(Convert.FromHexString("010000000000000080BA8C0101000000FFFFFFFF")), "weapon grant preserves full catalog ID separately from the inventory handle");
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
                var calls = GameActions.ItemCalls(root, root + 0x5790040, root + 0x5700000, dispatcher.Payload);
                Check(calls[0].Function == root + 0x1A86FF0 && calls[0].Arguments.SequenceEqual(new ulong[] { dispatcher.Payload + 4, root + 0x5700000, dispatcher.Payload + 8 }) &&
                    calls[1].Function == root + 0x17D89F0 && calls[1].Arguments.SequenceEqual(new ulong[] { root + 0x5790040, dispatcher.Payload }) &&
                    calls[2].Function == root + 0x1A8BC00 && calls[2].Arguments.SequenceEqual(new ulong[] { dispatcher.Payload + 4 }),
                    "item request constructs a handle from the catalog ID, awards, then releases temporary ownership");
                ulong[] fixtureFunctions = [root + 0x5780300, root + 0x5780000, root + 0x5780400];
                dispatcher.ExecuteAsync(calls.Select((call, i) => call with { Function = fixtureFunctions[i] }).ToArray(), payload).GetAwaiter().GetResult();
                Check(memory.Int32(root + 0x5790040) == 0x400003E8 && memory.Int32(root + 0x5790044) == 20 &&
                    unchecked((uint)memory.Int32(root + 0x5790048)) == 0xB00003E8 && memory.Int32(dispatcher.Payload + 4) == 0,
                    "native award receives a constructed goods handle before temporary ownership is released");
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
            memory.Write(world + 0x60, BitConverter.GetBytes(player.Actor));
            CheckLoadingSelections(memory, session, root, world, player);
            child.StandardInput.WriteLine(); child.WaitForExit(5000);
            Check(!memory.IsAlive, "detect process exit");
            Reject<IOException>(() => memory.SetInt32(player.Component + 0xF8, 1), "reject writes after process exit");
        }
        finally { if (!child.HasExited) child.Kill(); }
    }
    private static void CheckLoadingSelections(ProcessMemory memory, PlayerSession session, ulong root, ulong world, PlayerState player)
    {
        _ = new Application();
        var window = new MainWindow();
        var binding = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        void Field(string name, object? value) => typeof(MainWindow).GetField(name, binding)!.SetValue(window, value);
        void Poll() => typeof(MainWindow).GetMethod("Poll", binding)!.Invoke(window, [null, EventArgs.Empty]);
        string[] names = ["KeepHealth", "InfiniteStamina", "GodMode", "OneShot", "Stealth", "Silent", "NoAi", "NoFallDamage"];
        var toggles = names.Select(name => (System.Windows.Controls.CheckBox)window.FindName(name)).ToArray();
        Field("updating", true);
        foreach (var toggle in toggles) toggle.IsChecked = true;
        Field("updating", false); Field("session", session); Field("memory", memory);
        Poll();
        memory.Write(world + 0x60, new byte[8]);
        Poll();
        Check(toggles.All(x => x.IsChecked == true), "UI preserves all selected practice toggles across a null-player loading screen");
        var replacement = root + 0x57A0000;
        memory.Write(replacement, memory.Read(player.Component, 0x208));
        memory.Write(replacement + 0x200, [0x10]);
        var modules = memory.Pointer(player.Actor + 0x3B0);
        memory.Write(modules + 0x20, BitConverter.GetBytes(replacement));
        memory.Write(world + 0x60, BitConverter.GetBytes(player.Actor));
        memory.Write(root + 0x553E889, [0]); // Simulate the engine resetting a global flag on load.
        var oldFlags = memory.Read(player.Component + 0x200, 1)[0];
        Poll();
        Check(memory.Read(replacement + 0x200, 1)[0] == 0x9B && memory.Read(root + 0x553E889, 1)[0] == 1 && memory.Read(root + 0x553E895, 1)[0] == 1,
            "UI reapplies god mode, stamina and one-shot to the loaded player");
        Check(memory.Read(player.Component + 0x200, 1)[0] == oldFlags, "transition never writes through the old resource component");
        typeof(MainWindow).GetMethod("StopFeatures", binding)!.Invoke(window, null);
        Check(toggles.All(x => x.IsChecked == false) && memory.Read(replacement + 0x200, 1)[0] == 0x10,
            "explicit stop clears selections and restores the new component's original bits");
        Field("session", null); Field("memory", null); window.Close();
        memory.Write(modules + 0x20, BitConverter.GetBytes(player.Component));
    }
    private static int Render(string path, int width, int height, string page, double scrollOffset = 0)
    {
        var app = new Application();
        var window = new MainWindow { Width = width, Height = height };
        var nav = (System.Windows.Controls.Button)window.FindName("Nav" + page);
        nav.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, height)); content.Arrange(new Rect(0,0,width,height)); content.UpdateLayout();
        ((System.Windows.Controls.ScrollViewer)window.FindName("PageScroll")).ScrollToVerticalOffset(scrollOffset);
        content.UpdateLayout();
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
