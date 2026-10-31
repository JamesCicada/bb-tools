using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using BBTools.LinuxProbe;
using BBTools.LinuxCore;
using BBTools.Memory;

internal static class Program
{
    [DllImport("libc",SetLastError=true)] private static extern nint mmap(nint address,nuint length,int protection,int flags,int fd,nint offset);
    [DllImport("libc",SetLastError=true)] private static extern int mprotect(nint address,nuint length,int protection);
    [DllImport("libc")] private static extern int munmap(nint address,nuint length);
    private static int count;
    private static void Check(bool value, string name)
    { if (!value) throw new Exception(name); count++; Console.WriteLine($"PASS {name}"); }
    private static void Reject<T>(Action action, string name) where T : Exception
    { try { action(); } catch (T) { Check(true, name); return; } throw new Exception(name); }
    private static readonly byte[] FixtureBytes = Enumerable.Range(0, 64).Select(i => (byte)(i ^ 0xA5)).ToArray();
    public static async Task<int> Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--fixture" }))
        {
            var allocation = mmap(0,8192,3,0x22,-1,0);
            if(allocation==-1)throw new IOException("Fixture mmap failed");
            var game=CreateGameFixture();
            try { Marshal.Copy(FixtureBytes,0,allocation,64);if(mprotect(allocation+4096,4096,5)!=0)throw new IOException("Fixture mprotect failed");Console.WriteLine(allocation.ToString("X")+" "+game.ToString("X"));Console.ReadLine();var final=new byte[64];Marshal.Copy(allocation,final,0,64);if(!final.SequenceEqual(FixtureBytes))return 2; }
            finally { munmap(allocation,8192);munmap(game,0x5800000); }
            return 0;
        }
        try
        {
            var code = LinuxMap.Parse("0000000100000000-0000000100200000 r-xp 00000000 00:00 0 /tmp/game with spaces (deleted)");
            Check(code.Start==0x100000000&&code.End==0x100200000&&code.ScanCode&&code.Path=="/tmp/game with spaces (deleted)","parse full-width Linux mappings and paths containing spaces");
            Check(!LinuxMap.Parse("10000-20000 rw-s 0000 00:00 0").ScanCode,"data maps are excluded from code scanning");
            foreach(var invalid in new[]{"","100-80 r-xp 0 00:00 0","x-200 r-xp 0 00:00 0","100-200 abcq 0 00:00 0"})
                Reject<IOException>(()=>LinuxMap.Parse(invalid),"reject malformed process map");
            var stat="42 (a tricky ) process) S "+string.Join(' ',Enumerable.Range(4,18))+" 987654 0 0";
            Check(LinuxProcessMemory.ParseIdentity(stat)=="987654","process identity reads field 22 despite spaces and parentheses in comm");
            Reject<IOException>(()=>LinuxProcessMemory.ParseIdentity(stat.Replace(") S ",") Z ")),"zombie processes cannot be inspected");
            Check(BBTools.LinuxProbe.Program.Rank("bb-probe")>BBTools.LinuxProbe.Program.Rank("bbport")&&BBTools.LinuxProbe.Program.Rank("other")==0,"rank native bbport processes without requiring an exe suffix");
            var processes=new[]{new LinuxCandidate(2,"bb-probe","10"),new LinuxCandidate(1,"launcher","20"),new LinuxCandidate(3,"shadps4","30")};
            Check(LinuxProcessCatalog.Filter(processes,"").Select(x=>x.Pid).SequenceEqual(new[]{2,3,1})&&LinuxProcessCatalog.Filter(processes,"0x2").Single().Pid==2,"desktop process filtering preserves all processes and ranks likely game engines first");
            Check(LinuxPermissions.Explain("1").Contains("child/descendant")&&LinuxPermissions.Explain("3").Contains("cannot bypass"),"permission guidance distinguishes parent launch from an unbypassable no-tracing policy");
            var launchFile=Path.GetTempFileName();
            try{
                var launch=LinuxProcessCatalog.CreateStartInfo(launchFile,"[\"--play\",\"a path with spaces\",\"$(not-a-command)\"]");
                Check(!launch.UseShellExecute&&launch.ArgumentList.Count==3&&launch.ArgumentList[1]=="a path with spaces"&&launch.ArgumentList[2]=="$(not-a-command)","launcher arguments are separate literal values and never interpreted as shell code");
                Reject<ArgumentException>(()=>LinuxProcessCatalog.CreateStartInfo("relative-file","[]"),"launcher rejects relative executable paths");
            }finally{File.Delete(launchFile);}
            var fixture=new SparseMemory();const ulong image=0x40000000;
            foreach(var f in PlayerSession.Fingerprints)fixture.Store(image+f.Offset,f.Bytes);
            var anchor=PlayerSession.Fingerprints[0];
            // Put the anchor across a scan-chunk boundary inside a coalesced map.
            var start=image+anchor.Offset-1024*1024+8;
            var maps=new[]{new LinuxMap(start,start+1024*1024+64,"r-xp","")};
            Check(LinuxImageLocator.Find(fixture,maps,TimeSpan.FromSeconds(5))==image,"find a relocated guest image through a chunk-boundary anchor and all seven fingerprints");
            fixture.Store(image+PlayerSession.Fingerprints[1].Offset,[0]);
            Reject<IOException>(()=>LinuxImageLocator.Find(fixture,maps,TimeSpan.FromSeconds(5)),"reject an anchor when the remaining profile differs");
            fixture.Store(image+PlayerSession.Fingerprints[1].Offset,PlayerSession.Fingerprints[1].Bytes);
            const ulong otherImage=0x80000000;
            foreach(var f in PlayerSession.Fingerprints)fixture.Store(otherImage+f.Offset,f.Bytes);
            var second=new LinuxMap(otherImage+anchor.Offset,otherImage+anchor.Offset+64,"r-xp","");
            Reject<IOException>(()=>LinuxImageLocator.Find(fixture,maps.Append(second),TimeSpan.FromSeconds(5)),"ambiguous supported images are rejected");
            Reject<IOException>(()=>LinuxImageLocator.Find(fixture,maps,TimeSpan.FromTicks(-1)),"image scan respects its deadline");
            using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();Reject<OperationCanceledException>(()=>LinuxImageLocator.Find(fixture,maps,TimeSpan.FromSeconds(5),cancelled.Token),"guest-image discovery is cancellable before a window closes");}
            if(OperatingSystem.IsLinux()&&RuntimeInformation.ProcessArchitecture==Architecture.X64)await NativeChecks();
            else Console.WriteLine("SKIP Linux syscall checks: this host is not Linux x64.");
            Console.WriteLine($"PASS: {count} Linux-port foundation checks.");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true")Console.WriteLine("::error::"+ex.ToString().Replace("%","%25").Replace("\r","%0D").Replace("\n","%0A"));return 1;}
    }
    private static async Task NativeChecks()
    {
        var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true};
        if(Path.GetFileNameWithoutExtension(Environment.ProcessPath)=="dotnet")start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--fixture");
        using var child=Process.Start(start)??throw new IOException("Could not start the isolated memory fixture.");
        try
        {
            var locations=(await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)))!.Split(' ');
            var address=ulong.Parse(locations[0],NumberStyles.HexNumber,CultureInfo.InvariantCulture);
            var memory=new LinuxProcessMemory(child.Id);
            Check(memory.Read(address,64).SequenceEqual(FixtureBytes),"read exact bytes from an isolated Linux child using process_vm_readv");
            Check(memory.Maps().Any(m=>m.Start<=address&&address<m.End),"proc maps include the child's known allocation");
            Reject<NotSupportedException>(()=>memory.Write(address,[0]),"Linux probe refuses all memory writes");
            memory.WritesEnabled=true;memory.Write(address,[0x42]);
            Check(memory.Read(address,1)[0]==0x42,"explicitly enabled Linux process_vm_writev edits only the controlled child's writable data");
            memory.Write(address,FixtureBytes);
            var resumed=false;
            memory.Suspended(()=>memory.Suspended(()=>resumed=true));
            Check(resumed,"native Linux suspension nests and releases every seized thread");
            Reject<InvalidOperationException>(()=>memory.Suspended(()=>throw new InvalidOperationException("fixture")),"native suspension propagates action failures after releasing threads");
            Check(memory.Read(address,64).SequenceEqual(FixtureBytes),"failed paused operations leave the child readable");
            var remoteData=memory.AllocateData(4096);memory.Write(remoteData,[1,2,3,4]);
            Check(memory.Read(remoteData,4).SequenceEqual(new byte[]{1,2,3,4}),"remote mmap allocates writable game data without touching ordinary code");
            var remoteCode=memory.AllocateCode([0xC3]);
            Check(memory.IsExecutable(remoteCode,1)&&memory.Read(remoteCode,1)[0]==0xC3,"remote mmap/mprotect finalizes a read/execute dispatcher page");
            Reject<IOException>(()=>memory.Write(remoteCode,[0xCC]),"ordinary writes cannot alter finalized native code pages");
            Reject<IOException>(()=>memory.Write(address+4096,[0x42]),"Linux writer rejects executable mappings");
            Reject<IOException>(()=>memory.Write(address+4095,[1,2]),"Linux writer rejects a transfer crossing data and executable mappings");
            memory.WritesEnabled=false;Reject<NotSupportedException>(()=>memory.Write(address,[0]),"disabling experimental editing restores the read-only write gate");
            Reject<IOException>(()=>memory.Read(0,16),"invalid Linux addresses fail without returning partial data");
            var candidate=new LinuxCandidate(child.Id,"bb-probe",LinuxProcessMemory.ParseIdentity(File.ReadAllText($"/proc/{child.Id}/stat")));
            using(var game=new LinuxGameConnection(candidate))
            {
                var initial=game.ReadAndUpdate();
                Check(!game.Editing&&initial.Player.Health==100&&initial.Character.Values.Echoes==1200,"native Linux controller discovers a relocated synthetic guest and starts read-only");
                Reject<InvalidOperationException>(()=>game.SetEchoes(12345),"controller blocks edits before explicit opt-in");
                game.SetEditing(true);game.SetHealth(150);game.SetEchoes(12345);
                Check(game.Session.ReadPlayer().Health==150&&game.Session.ReadCharacter().Values.Echoes==12345,"shared health and Echoes actions work end to end through Linux data writes");
                game.SetAttributes([11,10,10,10,10,10],game.Session.ReadCharacter());
                Check(game.Session.ReadCharacter().Level==11&&game.Session.ReadCharacter().Values.Vitality==11,"Linux attribute edits preserve the shared Blood Level calculation and guards");
                using(var second=new LinuxGameConnection(candidate))Reject<IOException>(()=>second.SetEditing(true),"per-process edit ownership prevents a second trainer from taking over");
                game.KeepHealth=game.InfiniteStamina=true;game.Flags.Add(PracticeFlag.GodMode);game.Flags.Add(PracticeFlag.InfiniteItems);game.Flags.Add(PracticeFlag.NoHitReaction);game.Flags.Add(PracticeFlag.NoEchoesLoss);
                game.ReadAndUpdate();var p=game.Session.ReadPlayer();var inspector=new LinuxProcessMemory(child.Id);
                Check(p.Health==p.MaxHealth&&(inspector.Read(p.Component+0x200,1)[0]&0x83)==0x83&&(inspector.Read(p.Component+0x201,1)[0]&2)!=0,"Linux polling applies validated health, stamina and consumption/protection bits");
                game.SetEditing(false);
                Check(!game.Editing&&inspector.Read(p.Component+0x200,1)[0]==0x10&&inspector.Read(p.Component+0x201,1)[0]==0&&(inspector.Read(p.Actor+0x1E2,1)[0]&0x40)==0,"disabling editing restores only the original owned resource/combat bits");
                Reject<InvalidOperationException>(()=>game.SetHealth(100),"controller returns to a closed write gate after cleanup");
            }
            await child.StandardInput.WriteLineAsync("exit");await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            Check(child.ExitCode==0,"native child verifies its original bytes were restored before exit");
            Check(memory.HasExited(),"an exited game can release cleanup ownership without writing through its stale PID");
            Reject<IOException>(()=>memory.Read(address,64),"exited child process cannot be read through a stale PID");
        }
        finally{if(!child.HasExited){child.Kill();await child.WaitForExitAsync();}}
    }
    private static nint CreateGameFixture()
    {
        var root=mmap(0,0x5800000,3,0x22,-1,0);if(root==-1)throw new IOException("Synthetic game mmap failed");
        void Put(ulong offset,byte[] bytes)=>Marshal.Copy(bytes,0,root+(nint)offset,bytes.Length);
        void Ptr(ulong offset,ulong value)=>Put(offset,BitConverter.GetBytes((ulong)root+value));
        void Int(ulong offset,int value)=>Put(offset,BitConverter.GetBytes(value));
        const ulong world=0x5700000,actor=0x5710000,modules=0x5720000,stats=0x5730000,data=0x5740000,manager=0x5750000;
        Ptr(PlayerSession.WorldPointer,world);Ptr(world+0x60,actor);Ptr(actor+0x3B0,modules);Ptr(modules+0x20,stats);Ptr(stats+8,actor);Ptr(actor+0x3C0,data);Ptr(0x553B130,manager);Ptr(manager+8,data);
        Int(actor+8,123);Int(stats+0xF8,100);Int(stats+0xFC,200);Int(stats+0x134,70);Int(stats+0x138,100);Put(stats+0x200,[0x10]);
        foreach(var offset in new ulong[]{0x40,0x48,0x58,0x60,0x68,0x70})Int(data+offset,10);Int(data+0x90,10);Int(data+0x94,1200);
        foreach(var f in PlayerSession.Fingerprints.Concat(PlayerSession.FeatureFingerprints))Put(f.Offset,f.Bytes);
        foreach(var page in PlayerSession.Fingerprints.Concat(PlayerSession.FeatureFingerprints).Select(f=>f.Offset&~4095UL).Distinct())
            if(mprotect(root+(nint)page,4096,5)!=0)throw new IOException("Synthetic code protection failed");
        return root;
    }
    private sealed class SparseMemory : IMemory
    {
        private readonly Dictionary<ulong,byte> bytes=new();
        public void Store(ulong at,byte[] value){for(var i=0;i<value.Length;i++)bytes[at+(ulong)i]=value[i];}
        public byte[] Read(ulong at,int size){var result=new byte[size];for(var i=0;i<size;i++)result[i]=bytes.GetValueOrDefault(at+(ulong)i);return result;}
        public void Write(ulong at,byte[] value)=>throw new Exception("Read-only inspection attempted a write.");
    }
}
