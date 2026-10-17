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
        try { LibraryChecks(); UpdateChecks(); Integration(); Console.WriteLine($"PASS: {checks} checks (update service and real synthetic Windows process)."); return 0; }
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
    private static void CameraChecks(ProcessMemory memory, PlayerSession session, ulong root)
    {
        foreach(var f in CameraFov.Fingerprints) memory.Write(root+f.Offset,f.Bytes);
        memory.Write(root+CameraFov.ClampOffset,CameraFov.StandardClamp);
        var repo=memory.AllocateData(0x2000); var cap=memory.AllocateData(0x100); var handle=memory.AllocateData(0x100);
        var file=memory.AllocateData(0x200); var name=memory.AllocateData(0x100);
        void Ptr(ulong at,ulong value)=>memory.Write(at,BitConverter.GetBytes(value));
        Ptr(root+0x5540340,repo); var slot=repo+0x70+0x21*0x48;
        memory.SetInt32(slot,1); Ptr(slot+8,cap); Ptr(cap+0x18,name); Ptr(cap+0x70,handle); Ptr(handle+0x70,file);
        memory.Write(name,System.Text.Encoding.Unicode.GetBytes("LockCamParam\0"));
        var header=new byte[0x40]; BitConverter.GetBytes((ushort)2).CopyTo(header,10);
        System.Text.Encoding.ASCII.GetBytes("LOCK_CAM_PARAM_ST\0").CopyTo(header,12); header[0x2D]=0x85;header[0x2E]=2;
        memory.Write(file,header);
        memory.SetInt32(file+0x40,0);Ptr(file+0x48,0x70);memory.SetInt32(file+0x58,1);Ptr(file+0x60,0x90);
        memory.Write(file+0x84,BitConverter.GetBytes(43f));memory.Write(file+0xA4,BitConverter.GetBytes(48f));
        var executableImage = new byte[(int)CameraFov.ClampOffset+256];
        foreach(var fingerprint in CameraFov.Fingerprints) fingerprint.Bytes.CopyTo(executableImage,(int)fingerprint.Offset);
        CameraFov.StandardClamp.CopyTo(executableImage,(int)CameraFov.ClampOffset);
        var executableBase=memory.AllocateCode(executableImage);
        Reject<IOException>(()=>memory.Write(executableBase+CameraFov.ClampOffset,CameraFov.UncappedClamp),"generic memory writes still reject executable pages");
        memory.Suspended(()=>memory.WriteCameraFovClamp(executableBase,true));
        Check(memory.Read(executableBase+CameraFov.ClampOffset,4).SequenceEqual(CameraFov.UncappedClamp),"narrow camera clamp primitive updates a read-execute page");
        Reject<IOException>(()=>memory.Write(executableBase+CameraFov.ClampOffset,CameraFov.StandardClamp),"camera clamp update restores original read-execute protection");
        memory.Suspended(()=>memory.WriteCameraFovClamp(executableBase,false));
        Check(memory.Read(executableBase+CameraFov.ClampOffset,4).SequenceEqual(CameraFov.StandardClamp),"read-execute camera clamp restores exactly");
        var fov=new CameraFov(memory,session);
        Check(fov.Maximum==120,"standard camera supports the 120-degree vertical slider");
        Reject<ArgumentException>(()=>fov.Apply(121),"FOV beyond the supported 120-degree range is rejected");
        fov.Apply(100);
        Check(memory.Read(root+CameraFov.ClampOffset,4).SequenceEqual(CameraFov.UncappedClamp) && BitConverter.ToSingle(memory.Read(file+0x84,4))==100,"100-degree FOV automatically removes the standard clamp");
        fov.Apply(120);Check(BitConverter.ToSingle(memory.Read(file+0x84,4))==120,"120-degree FOV applies to camera parameters");
        fov.Restore();Check(memory.Read(root+CameraFov.ClampOffset,4).SequenceEqual(CameraFov.StandardClamp),"reset restores the clamp installed by this app");
        fov.Apply(46);Check(BitConverter.ToSingle(memory.Read(file+0x84,4))==46 && BitConverter.ToSingle(memory.Read(file+0xA4,4))==46,"FOV override updates all validated camera rows");
        fov.Apply(47);fov.Restore();Check(BitConverter.ToSingle(memory.Read(file+0x84,4))==43 && BitConverter.ToSingle(memory.Read(file+0xA4,4))==48,"FOV reset restores each individual original value after slider changes");
        memory.Write(root+CameraFov.ClampOffset,CameraFov.UncappedClamp); fov.Apply(90);
        Check(fov.Maximum==120 && BitConverter.ToSingle(memory.Read(file+0x84,4))==90,"verified runtime uncap enables wider angles without patching instructions");
        memory.Write(file+0x84,BitConverter.GetBytes(60f));
        Reject<IOException>(()=>fov.Apply(80),"foreign FOV edits pause ownership instead of overwriting another mod");
        fov.Restore();Check(memory.Read(root+CameraFov.ClampOffset,4).SequenceEqual(CameraFov.UncappedClamp),"preexisting external uncap patch survives reset");
        Check(BitConverter.ToSingle(memory.Read(file+0x84,4))==60 && BitConverter.ToSingle(memory.Read(file+0xA4,4))==48,"restore preserves foreign edits and restores owned rows");
        memory.Write(root+CameraFov.ClampOffset,[0,0,0,0]);Reject<IOException>(()=>fov.Apply(45),"unknown FOV camera patch is rejected");
        memory.Write(root+CameraFov.ClampOffset,CameraFov.StandardClamp);
        Reject<ArgumentException>(()=>fov.Apply(float.NaN),"nonfinite FOV is rejected before writes");
        fov.Apply(45);
        var replacement=memory.AllocateData(0x200);memory.Write(replacement,memory.Read(file,0x200));
        memory.Write(replacement+0x84,BitConverter.GetBytes(41f));memory.Write(replacement+0xA4,BitConverter.GetBytes(47f));
        Ptr(handle+0x70,replacement);fov.Apply(46);
        Check(BitConverter.ToSingle(memory.Read(file+0x84,4))==45 && BitConverter.ToSingle(memory.Read(replacement+0x84,4))==46,"new camera table resumes FOV without writing through old allocations");
        fov.Restore();Check(BitConverter.ToSingle(memory.Read(replacement+0x84,4))==41 && BitConverter.ToSingle(memory.Read(replacement+0xA4,4))==47,"post-load restoration uses the replacement table's original values");
        memory.Write(replacement+12,[0]);Reject<IOException>(()=>fov.Apply(45),"wrong camera parameter type blocks FOV writes");
    }
    private static void LibraryChecks()
    {
        var folder = Path.Combine(Path.GetTempPath(), "BBTools-library-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var legacy = Path.Combine(folder,"locations.json"); var path = Path.Combine(folder,"library.db");
            var spot = new SavedLocation("Test warp",0x15000000,1,2,3,4);
            new LocationBook([spot]).Save(legacy);
            var library = new UserLibrary(path,legacy);
            Check(library.Locations().SequenceEqual(new[] { spot }) && File.Exists(legacy), "SQLite migration preserves bookmark and original JSON");
            var changed = spot with { Name="TEST WARP", X=99 }; library.Save(changed);
            Check(new UserLibrary(path,legacy).Locations().Single() == changed, "SQLite reopen does not repeat migration or overwrite newer bookmarks");
            library.Delete("location",changed.Name);
            Check(new UserLibrary(path,legacy).Locations().Length == 0,"deleted migrated bookmarks are not resurrected");
            var preset = new PracticePreset("Boss practice",true,true,["GodMode","InfiniteItems"],2,.5f);
            library.Save(preset);
            var loaded = new UserLibrary(path,legacy).Presets().Single();
            Check(loaded.Name == preset.Name && loaded.Flags.SequenceEqual(preset.Flags) && loaded.Damage == 2 && loaded.Echoes == .5f && loaded.KeepHealth && loaded.Stamina,"preset roundtrip retains combat options and multipliers");
            library.Save(preset with { Name="BOSS PRACTICE", Damage=3 });
            Check(library.Presets().Single().Damage == 3,"preset names replace case-insensitively");
            Reject<ArgumentException>(() => library.Save(preset with { Damage=float.NaN }),"nonfinite multiplier rejected before database write");
            Reject<ArgumentException>(() => library.Save(preset with { Flags=["NotAFlag"] }),"unknown preset flag rejected");
            Reject<ArgumentException>(() => library.Save(preset with { Name=" " }),"empty preset name rejected");
            Check(library.Presets().Single().Damage == 3,"invalid preset writes preserve existing data");
            var quoted = spot with { Name="Hunter's ; DROP TABLE entries; --" }; library.Save(quoted);
            Check(library.Locations().Single() == quoted,"parameterized SQL preserves punctuation in bookmark names");
            library.Delete("preset",preset.Name); Check(library.Presets().Length == 0,"preset deletion persists");
            File.WriteAllText(legacy,"invalid JSON");
            Reject<JsonException>(() => new UserLibrary(Path.Combine(folder,"bad.db"),legacy),"malformed legacy file is reported without marking migration complete");
            new LocationBook([spot]).Save(legacy);
            Check(new UserLibrary(Path.Combine(folder,"bad.db"),legacy).Locations().Single() == spot,"failed migration can retry successfully after repair");
        }
        finally { Directory.Delete(folder,true); }
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
            // Native-owned effect stand-ins: insert a preallocated node, then activate it.
            Write(root + 0x17E3130, Convert.FromHexString("488B0748894708C740400F270000C3"));
            Write(root + 0x17EA980, Convert.FromHexString("C7471C0400000031C0C3"));
            VirtualProtect((nint)(root + 0x17E3000), 4096, 0x20, out _);
            VirtualProtect((nint)(root + 0x17EA000), 4096, 0x20, out _);
            Write(root + 0x5780500, Convert.FromHexString("488B06488907F083400801C3"));
            Write(root + 0x1BB27A0, Convert.FromHexString("488B02488986880A0000F08340080148C70700000000C3"));
            VirtualProtect((nint)(root + 0x1BB2000), 4096, 0x20, out _);
            Write(root + 0x13CFD80, Convert.FromHexString("81FE3C23000075048B4704C38B07C3"));
            Write(root + 0x156D110, [0xC3]); Write(root + 0x156D0E0, [0xC3]);
            Write(root + 0x132E050, Convert.FromHexString("48B8").Concat(BitConverter.GetBytes(root + 0x57F0100)).Concat(Convert.FromHexString("8930C3")).ToArray());
            Write(root + 0x13D0060, Convert.FromHexString("897708C3"));
            foreach (var page in new ulong[] { 0x13CF000, 0x156D000, 0x132E000, 0x13D0000 }) VirtualProtect((nint)(root + page), 4096, 0x20, out _);
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
            CameraChecks(memory,session,root);
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
            memory.Write(player.Component + 0x201, [0x40]);
            session.SetPracticeFlag(PracticeFlag.InfiniteItems, true);
            Check(memory.Read(player.Component + 0x201, 1)[0] == 0x42, "infinite consumables and bullets preserve adjacent player flags");
            session.RestorePracticeFlag(PracticeFlag.InfiniteItems);
            Check(memory.Read(player.Component + 0x201, 1)[0] == 0x40, "restore original item consumption bit");
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
            Check(BossDestinations.All.Length == 22 && BossDestinations.All.Count(x => x.Name.StartsWith("[DLC]")) == 5 && BossDestinations.All.All(x => GameActions.Lamps.Any(l => l.Entity == x.EntryLamp)),
                "twenty-two boss entrance presets include all five DLC bosses and valid area lamps");
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
                var neutral = new byte[0x210]; neutral[0x141] = 0xFF;
                for (var i = 0; i < 4; i++) BitConverter.GetBytes(1f).CopyTo(neutral, 0x38 + i * 4);
                BitConverter.GetBytes(1f).CopyTo(neutral, 0xE0);
                neutral[0x155] = 0xAB;
                var modified = GameActions.MultiplierRow(neutral, 2.5f, 3f);
                Check(BitConverter.ToSingle(modified, 0x38) == 2.5f && BitConverter.ToSingle(modified, 0xE0) == 3 &&
                    modified[0x155] == 0xAB && BitConverter.ToSingle(neutral, 0x38) == 1,
                    "multiplier row scales outgoing damage and enemy rewards while preserving other fields and the original row");
                Reject<ArgumentException>(() => GameActions.MultiplierRow(neutral, float.NaN, 1), "reject nonfinite multipliers");
                Reject<ArgumentException>(() => GameActions.MultiplierRow(neutral, 1, 101), "reject excessive reward multiplier");
                var container = root + 0x57C1000; var node = root + 0x57C0000; var originalRow = root + 0x57C2000;
                var cloneRow = memory.AllocateData(0x210); memory.Write(cloneRow, modified);
                memory.Write(player.Actor + 0x1C8, BitConverter.GetBytes(container));
                memory.Write(container, BitConverter.GetBytes(node)); memory.Write(container + 0x18, BitConverter.GetBytes(player.Actor));
                memory.Write(node + 0x48, BitConverter.GetBytes(originalRow));
                var effectPayload = new byte[64];
                ulong[] effectFields = [player.Actor, container, cloneRow, originalRow];
                for (var i = 0; i < effectFields.Length; i++) BitConverter.GetBytes(effectFields[i]).CopyTo(effectPayload, i * 8);
                var helper = memory.AllocateCode(GameActions.BuildEffectHelper(root));
                dispatcher.ExecuteAsync([new(helper, dispatcher.Payload)], effectPayload).GetAwaiter().GetResult();
                Check(memory.Int32(dispatcher.Payload + 48) == 1 && memory.Pointer(node + 0x48) == cloneRow &&
                    memory.Pointer(dispatcher.Payload + 56) == node && BitConverter.ToSingle(memory.Read(node, 4)) == -1 && memory.Int32(node + 0x1C) == 4,
                    "game-thread effect helper attaches the private row only to the native-created player node and activates it");
                dispatcher.ExecuteAsync([new(helper, dispatcher.Payload)], effectPayload).GetAwaiter().GetResult();
                Check(memory.Int32(dispatcher.Payload + 48) == 0 && memory.Pointer(node + 0x48) == cloneRow,
                    "effect ID collision cannot overwrite an existing effect");
                memory.Write(container + 8, new byte[8]);
                memory.Write(container + 0x18, BitConverter.GetBytes(player.Actor + 8));
                dispatcher.ExecuteAsync([new(helper, dispatcher.Payload)], effectPayload).GetAwaiter().GetResult();
                Check(memory.Int32(dispatcher.Payload + 48) == 0 && BitConverter.ToUInt64(memory.Read(container + 8, 8)) == 0,
                    "changed effect container owner blocks native insertion");
                memory.Write(container + 0x18, BitConverter.GetBytes(player.Actor));
                memory.Write(container + 8, BitConverter.GetBytes(node));
                using (var multiplierActions = new GameActions(memory, session))
                {
                    var binding = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    foreach (var field in new (string Name, ulong Value)[] { ("effectActor", player.Actor), ("effectContainer", container),
                        ("effectNode", node), ("effectRow", cloneRow), ("originalEffectRow", originalRow) })
                        typeof(GameActions).GetField(field.Name, binding)!.SetValue(multiplierActions, field.Value);
                    Check(multiplierActions.MultipliersAttached(), "multiplier ownership requires the current actor, native node and private row");
                    multiplierActions.ResetMultipliers();
                    Check(memory.Pointer(node + 0x48) == originalRow && memory.Int32(node) == 0 && memory.Read(node + 0x2D, 1)[0] == 1 &&
                        !multiplierActions.MultipliersAttached(), "disabling multipliers restores the original row and schedules native node expiration");
                }
                var menuManager = root + 0x57D0000; var menuStep = root + 0x57D2000;
                memory.Write(root + 0x5562870, BitConverter.GetBytes(menuManager));
                memory.Write(menuManager + 0x48, BitConverter.GetBytes(menuStep));
                var menuPayload = new byte[96];
                BitConverter.GetBytes(menuManager).CopyTo(menuPayload, 0);
                BitConverter.GetBytes(root + 0x5780500).CopyTo(menuPayload, 8);
                var menuHelper = memory.AllocateCode(GameActions.BuildMenuHelper(root));
                dispatcher.ExecuteAsync([new(menuHelper, dispatcher.Payload)], menuPayload).GetAwaiter().GetResult();
                Check(memory.Int32(dispatcher.Payload + 80) == 1 && memory.Pointer(menuManager + 0xA88) == menuStep &&
                    memory.Int32(menuStep + 8) == 1 && BitConverter.ToUInt64(memory.Read(dispatcher.Payload + 16, 8)) == 0,
                    "remote menu helper transfers native step ownership to MenuMan and releases its temporary reference");
                dispatcher.ExecuteAsync([new(menuHelper, dispatcher.Payload)], menuPayload).GetAwaiter().GetResult();
                Check(memory.Int32(dispatcher.Payload + 80) == 0 && memory.Int32(menuStep + 8) == 1,
                    "opening another remote menu cannot replace an existing game menu");
                memory.Write(menuManager + 0xA88, new byte[8]);
                memory.Write(root + 0x5562870, BitConverter.GetBytes(menuManager + 8));
                dispatcher.ExecuteAsync([new(menuHelper, dispatcher.Payload)], menuPayload).GetAwaiter().GetResult();
                Check(memory.Int32(dispatcher.Payload + 80) == 0 && memory.Int32(menuStep + 8) == 1,
                    "menu manager replacement blocks stale menu construction");
                Check(GameActions.ChaliceLamps.Length == 9 && GameActions.ChaliceLamps.Select(l => l.Entity).Distinct().Count() == 9 &&
                    GameActions.ChaliceLamps.Last().Name == "Layer Four: Heart" && GameActions.ChaliceLamps[1].UnlockFlag == 12907830,
                    "chalice catalog includes all nine named seal/layer/heart destinations and their talk-script unlock flags");
                var flagManager = root + 0x57F0000;
                memory.Write(root + 0x553B100, BitConverter.GetBytes(flagManager));
                memory.SetInt32(flagManager, 123456); memory.SetInt32(flagManager + 4, 32);
                memory.SetInt32(player.Actor + 0x3F8, 0x15000000);
                var warpPayload = new byte[64];
                ulong[] warpFields = [flagManager, 123456, 32, 2902951, 1, 0, player.Actor, 94100000];
                for (var i = 0; i < warpFields.Length; i++) BitConverter.GetBytes(warpFields[i]).CopyTo(warpPayload, i * 8);
                var warpHelper = memory.AllocateCode(GameActions.BuildChaliceWarpHelper(root));
                dispatcher.ExecuteAsync([new(warpHelper, dispatcher.Payload)], warpPayload).GetAwaiter().GetResult();
                Check(memory.Int32(dispatcher.Payload + 40) == 1 && memory.Int32(root + 0x57F0100) == 2902951 && memory.Int32(flagManager + 8) == 9002,
                    "chalice helper validates generated dungeon/altar context and requests its lamp with the native destination bit");
                memory.SetInt32(root + 0x57F0100, 0); memory.SetInt32(flagManager, 654321);
                dispatcher.ExecuteAsync([new(warpHelper, dispatcher.Payload)], warpPayload).GetAwaiter().GetResult();
                Check(memory.Int32(dispatcher.Payload + 40) == 0 && memory.Int32(root + 0x57F0100) == 0,
                    "generated dungeon replacement prevents a chalice warp before return-point changes");
                memory.SetInt32(flagManager, 123456); memory.SetInt32(player.Actor + 0x3F8, 0x18010000);
                dispatcher.ExecuteAsync([new(warpHelper, dispatcher.Payload)], warpPayload).GetAwaiter().GetResult();
                Check(memory.Int32(dispatcher.Payload + 40) == 0 && memory.Int32(root + 0x57F0100) == 0,
                    "leaving Hunter's Dream prevents a stale chalice warp");
                var altarOwner = root + 0x57E0000; var altarArray = root + 0x57E1000;
                memory.Write(root + 0x553E9E8, BitConverter.GetBytes(altarOwner)); memory.SetInt32(altarOwner, 7);
                memory.Write(altarOwner + 8, BitConverter.GetBytes(altarArray));
                for (var bank = 1; bank <= 6; bank++)
                {
                    var gate = root + 0x57E2000 + (ulong)bank * 0x1000;
                    memory.Write(altarArray + (ulong)bank * 8, BitConverter.GetBytes(gate));
                    memory.Write(gate, BitConverter.GetBytes(root + 0x5336570));
                    memory.SetInt32(gate + 0x100, bank); memory.SetInt32(gate + 0x108 + 0xE0, 12345 + bank);
                    memory.SetInt32(gate + 0x108 + 0x25C, 4);
                }
                using (var altarActions = new GameActions(memory, session))
                {
                    var altars = altarActions.ReadChaliceAltars();
                    Check(altars.Length == 6 && altars.Select(a => a.MapUid).Distinct().Count() == 6 && altars.All(a => a.Status == 4),
                        "chalice travel enumerates all six character-owned dungeons without substituting fixed map IDs");
                    memory.SetInt32(altarOwner, 6);
                    Reject<IOException>(() => altarActions.ReadChaliceAltars(), "unloaded or incompatible altar owner blocks chalice traversal");
                }
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
                Check(memory.Int32(root + 0x5790008) == 123 && dispatcher.Result(2) == 123, "three-step native job preserves order and exposes its completed return value");
                dispatcher.ExecuteAsync([new(root + 0x5780200, root + 0x5790000, 0, player.Actor)]).GetAwaiter().GetResult();
                Check(memory.Pointer(root + 0x5790010) == character.Address, "recursive getter preserves result without repeating the request");
            }
            Check(memory.Pointer(player.Actor) == originalTable, "dispatcher detaches and restores original actor vtable");
            Reject<ArgumentException>(() => GameActions.ItemBatch(1, 0, 20), "equipment quantity restricted to one");
            var processList = new[] { new ProcessCandidate(55,"notepad",10), new ProcessCandidate(40,"shadPS4",10),
                new ProcessCandidate(30,"Bloodborne",10), new ProcessCandidate(20,"bbport_launcher",10),
                new ProcessCandidate(10,"bb-probe",10), new ProcessCandidate(0,"Idle",0),new ProcessCandidate(56,"Notepad",20) };
            var ranked = ProcessDiscovery.Filter(processList);
            Check(ranked.Length==processList.Length && ranked.Take(4).Select(x=>x.Id).SequenceEqual(new[] {10,30,20,40}),"all processes remain visible with native engine and likely game runtimes ranked first");
            Check(ranked.Where(x=>x.Name.Equals("notepad",StringComparison.OrdinalIgnoreCase)).Select(x=>x.Id).SequenceEqual(new[] {55,56}),"duplicate process names stay distinct and sort by PID");
            Check(ProcessDiscovery.Filter(processList,"NOTEpad").Length==2,"process filter is case-insensitive and searches unrelated apps");
            Check(ProcessDiscovery.Filter(processList,"bb probe").Single().Id==10,"multiple process search terms narrow the same process");
            Check(ProcessDiscovery.Filter(processList,"55").Single().Id==55 && ProcessDiscovery.Filter(processList,"0x37").Single().Id==55,"process filter supports decimal and hexadecimal PIDs");
            Check(ProcessDiscovery.Filter(processList,"no-such-process").Length==0 && ranked.Single(x=>x.Id==0).Started==0,"empty search results and restricted identities are retained correctly");
            Check(ProcessDiscovery.List().Any(x=>x.Id==Environment.ProcessId),"process discovery includes ordinary non-game processes");
            Check(ProcessDiscovery.FindBest([new(int.MaxValue,"notepad",1),new(int.MaxValue,"bb-probe",0)],()=>false)==null,"auto-connect skips unrelated apps and identities without creation time");
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
        string[] names = ["KeepHealth", "InfiniteStamina", "GodMode", "OneShot", "Stealth", "Silent", "NoAi", "NoFallDamage", "InfiniteItems"];
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
        memory.Write(replacement + 0x200, [0x10, 0x20]);
        var modules = memory.Pointer(player.Actor + 0x3B0);
        memory.Write(modules + 0x20, BitConverter.GetBytes(replacement));
        memory.Write(world + 0x60, BitConverter.GetBytes(player.Actor));
        memory.Write(root + 0x553E889, [0]); // Simulate the engine resetting a global flag on load.
        var oldFlags = memory.Read(player.Component + 0x200, 1)[0];
        Poll();
        Check(memory.Read(replacement + 0x200, 1)[0] == 0x9B && memory.Read(root + 0x553E889, 1)[0] == 1 && memory.Read(root + 0x553E895, 1)[0] == 1 && memory.Read(replacement + 0x201, 1)[0] == 0x22,
            "UI reapplies combat and consumption flags to the loaded player");
        Check(memory.Read(player.Component + 0x200, 1)[0] == oldFlags, "transition never writes through the old resource component");
        typeof(MainWindow).GetMethod("StopFeatures", binding)!.Invoke(window, null);
        Check(toggles.All(x => x.IsChecked == false) && memory.Read(replacement + 0x200, 1)[0] == 0x10 && memory.Read(replacement + 0x201, 1)[0] == 0x20,
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
        foreach (var fingerprint in PlayerSession.Fingerprints.Concat(PlayerSession.FeatureFingerprints).Concat(GameActions.Fingerprints).Concat(GameActions.MultiplierFingerprints).Concat(GameActions.MenuFingerprints).Concat(GameActions.ChaliceFingerprints).Concat(CameraFov.Fingerprints))
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
