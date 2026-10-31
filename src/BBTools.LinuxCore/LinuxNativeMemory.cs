using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BBTools.Memory;

namespace BBTools.LinuxCore;

// Native x86-64 only. All ptrace ownership is acquired/released synchronously
// on one OS thread. No ptrace scope, capabilities or system settings are changed.
public sealed partial class LinuxProcessMemory
{
    private readonly object nativeGate = new();
    private readonly Dictionary<int,int> stoppedThreads = [];
    private int nativeDepth;
    private bool disposed;
    public bool IsAlive => !disposed && !HasExited();
    public bool IsExecutable(ulong address,int size) => size>0 && address<0x800000000000 && (ulong)size<=0x800000000000-address &&
        Maps().Any(m=>m.Start<=address && address+(ulong)size<=m.End && m.ScanCode);
    private void RequireNative()
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(!WritesEnabled)throw new NotSupportedException("Enable game editing before using native game functions.");
        ValidateIdentity();
    }
    public void Suspended(Action operation)
    {
        lock(nativeGate)
        {
            RequireNative();
            if(nativeDepth>0){nativeDepth++;try{operation();}finally{nativeDepth--;}return;}
            var watch=Stopwatch.StartNew();
            try
            {
                // Stop every thread, including those created during enumeration.
                for(var pass=0;pass<32;pass++)
                {
                    var tids=Directory.GetDirectories($"/proc/{pid}/task").Select(Path.GetFileName).Select(x=>int.Parse(x!)).Order().ToArray();
                    if(tids.Length is <1 or >4096)throw new IOException("Invalid game thread count.");
                    foreach(var tid in tids.Where(t=>!stoppedThreads.ContainsKey(t)))
                    {
                        if(watch.Elapsed>TimeSpan.FromSeconds(3))throw new IOException("Timed out pausing game threads.");
                        var status=File.ReadAllText($"/proc/{pid}/task/{tid}/status");
                        if(status.Split('\n').Any(l=>l.StartsWith("TracerPid:")&&l.Split(':')[1].Trim()!="0"))
                            throw new IOException("Another debugger already controls a game thread. Close it before using native features.");
                        if(status.Split('\n').Any(l=>l.StartsWith("State:")&&l.Contains("T (stopped)")))
                            throw new IOException("The game is already job-stopped. Resume it before applying changes.");
                        if(Trace(0x4206,tid,0,0)<0)throw TraceError("seize game thread"); // SEIZE, no SIGSTOP
                        stoppedThreads[tid]=0;
                        if(Trace(0x4207,tid,0,0)<0)throw TraceError("interrupt game thread");
                        var signal=WaitStop(tid,watch,TimeSpan.FromSeconds(3));
                        stoppedThreads[tid]=signal==5?0:signal;
                        var tgid=File.ReadLines($"/proc/{tid}/status").First(l=>l.StartsWith("Tgid:")).Split(':')[1].Trim();
                        if(tgid!=pid.ToString())throw new IOException("Thread ownership changed during suspension.");
                    }
                    if(Directory.GetDirectories($"/proc/{pid}/task").All(p=>stoppedThreads.ContainsKey(int.Parse(Path.GetFileName(p)))))
                    {
                        nativeDepth=1;ValidateIdentity();operation();return;
                    }
                }
                throw new IOException("Game threads did not reach a stable pause.");
            }
            finally
            {
                nativeDepth=0;var failures=new List<int>();
                foreach(var (tid,signal) in stoppedThreads.Reverse())
                {
                    // A seize may have succeeded before an interrupt/wait failed.
                    // Interrupt/wait once more before detaching a still-running tracee.
                    if(Trace(17,tid,0,signal)<0 && Marshal.GetLastPInvokeError()!=3)
                    {
                        Trace(0x4207,tid,0,0);
                        try{WaitStop(tid,Stopwatch.StartNew(),TimeSpan.FromSeconds(2));}
                        catch(IOException){}
                        if(Trace(17,tid,0,signal)<0 && Marshal.GetLastPInvokeError()!=3)failures.Add(tid);
                    }
                }
                stoppedThreads.Clear();
                if(failures.Count!=0)throw new IOException("Could not release paused game threads: "+string.Join(",",failures));
            }
        }
    }
    private static int WaitStop(int tid,Stopwatch watch,TimeSpan timeout)
    {
        while(watch.Elapsed<timeout)
        {
            var result=WaitPid(tid,out var status,0x40000000|1); // __WALL | WNOHANG; never reap unrelated children
            if(result==tid)
            {
                if((status&0xff)==0x7f)return(status>>8)&0xff;
                throw new IOException("A game thread exited during the native request.");
            }
            if(result<0 && Marshal.GetLastPInvokeError()!=4)throw TraceError("wait for game thread");
            // System.Diagnostics.Process also waits on a launched child. Its
            // SIGCHLD reaper can consume a ptrace-stop notification. Confirm the
            // kernel stop directly instead of waiting forever for that event.
            if(result==0 && watch.ElapsedMilliseconds>30)
            {
                var info=Marshal.AllocHGlobal(128);
                try
                {
                    if(Trace(0x4202,tid,0,info)==0)
                    {
                        var signal=Marshal.ReadInt32(info);
                        if(signal is >0 and <65){WaitPid(tid,out _,0x40000000|1);return signal;}
                    }
                }
                finally{Marshal.FreeHGlobal(info);}
            }
            Thread.Sleep(1);
        }
        throw new IOException("Timed out waiting for a game thread stop.");
    }
    private static Exception TraceError(string action)
    {
        var error=Marshal.GetLastPInvokeError();
        return error is 1 or 13 ? new UnauthorizedAccessException("Linux denied permission to "+action+". "+LinuxPermissions.Explain()) :
            new IOException("Could not "+action+": "+new Win32Exception(error).Message);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Registers
    {
        public ulong R15,R14,R13,R12,Rbp,Rbx,R11,R10,R9,R8,Rax,Rcx,Rdx,Rsi,Rdi,OriginalRax,Rip,Cs,Flags,Rsp,Ss,FsBase,GsBase,Ds,Es,Fs,Gs;
    }
    private static Registers GetRegisters(int tid)
    {
        var pointer=Marshal.AllocHGlobal(Marshal.SizeOf<Registers>());
        try{if(Trace(12,tid,0,pointer)<0)throw TraceError("read game registers");return Marshal.PtrToStructure<Registers>(pointer);}
        finally{Marshal.FreeHGlobal(pointer);}
    }
    private static void SetRegisters(int tid,Registers value)
    {
        var pointer=Marshal.AllocHGlobal(Marshal.SizeOf<Registers>());
        try{Marshal.StructureToPtr(value,pointer,false);if(Trace(13,tid,0,pointer)<0)throw TraceError("restore game registers");}
        finally{Marshal.FreeHGlobal(pointer);}
    }
    private static ulong Peek(int tid,ulong address)
    {
        Marshal.SetLastPInvokeError(0);var value=Trace(1,tid,(nint)address,0);
        if(value==-1 && Marshal.GetLastPInvokeError()!=0)throw TraceError("read game instruction word");
        return unchecked((ulong)value);
    }
    private static void Poke(int tid,ulong address,ulong value)
    {if(Trace(4,tid,(nint)address,unchecked((nint)value))<0)throw TraceError("write game instruction word");}
    private ulong Syscall(ulong number,ulong first,ulong second=0,ulong third=0,ulong fourth=0,ulong fifth=0,ulong sixth=0)
    {
        if(nativeDepth==0)throw new InvalidOperationException("Remote syscalls require a paused game.");
        var tid=stoppedThreads.FirstOrDefault(x=>x.Value==0).Key;
        if(tid==0)throw new IOException("No signal-free game thread is available for a native request.");
        var original=GetRegisters(tid);var word=Peek(tid,original.Rip);var injected=false;
        try
        {
            // syscall; int3. Preserve the untouched bytes of the machine word.
            Poke(tid,original.Rip,(word&0xffffffffff000000UL)|0xCC050FUL);injected=true;
            var regs=original;regs.Rax=number;regs.OriginalRax=ulong.MaxValue;
            regs.Rdi=first;regs.Rsi=second;regs.Rdx=third;regs.R10=fourth;regs.R8=fifth;regs.R9=sixth;
            SetRegisters(tid,regs);
            if(Trace(7,tid,0,0)<0)throw TraceError("run native memory syscall");
            var signal=WaitStop(tid,Stopwatch.StartNew(),TimeSpan.FromSeconds(3));
            var result=GetRegisters(tid);
            if(signal!=5||result.Rip!=original.Rip+3)
            {stoppedThreads[tid]=signal==5?0:signal;throw new IOException("Native memory syscall stopped unexpectedly; the original context was restored.");}
            if(result.Rax>=unchecked((ulong)-4095L))throw new IOException("Native memory syscall failed: "+new Win32Exception(unchecked(-(int)result.Rax)).Message);
            return result.Rax;
        }
        finally
        {
            // Ensure it is stopped before touching the saved context on timeout.
            // GETREGS succeeds only in ptrace-stop; a running thread must stop first.
            try{GetRegisters(tid);}catch(IOException){Trace(0x4207,tid,0,0);WaitStop(tid,Stopwatch.StartNew(),TimeSpan.FromSeconds(2));}
            try{if(injected)Poke(tid,original.Rip,word);}finally{SetRegisters(tid,original);}
        }
    }
    public ulong AllocateData(int size)
    {
        if(size is <1 or >16*1024*1024)throw new ArgumentOutOfRangeException(nameof(size));
        ulong address=0;Suspended(()=>address=Syscall(9,0,(ulong)size,3,0x22,ulong.MaxValue,0));return address;
    }
    public ulong AllocateCode(byte[] code)
    {
        if(code.Length is <1 or >1024*1024)throw new ArgumentOutOfRangeException(nameof(code));
        ulong address=0;Suspended(()=>{address=Syscall(9,0,(ulong)code.Length,3,0x22,ulong.MaxValue,0);Write(address,code);Syscall(10,address,(ulong)code.Length,5);});return address;
    }
    private void ReplaceCode(ulong image,ulong offset,byte[] expected,byte[] replacement)
    {
        if(expected.Length!=replacement.Length || expected.Length is <1 or >128)throw new ArgumentException("Invalid protected patch size.");
        Suspended(()=>
        {
            var address=checked(image+offset);
            if(!Read(address,expected.Length).SequenceEqual(expected)||!IsExecutable(address,expected.Length))throw new IOException("Native instructions changed; no code was replaced.");
            var tid=stoppedThreads.First().Key;
            for(var i=0;i<replacement.Length;)
            {
                var at=(address+(ulong)i)&~7UL;var word=BitConverter.GetBytes(Peek(tid,at));var begin=(int)((address+(ulong)i)-at);var n=Math.Min(8-begin,replacement.Length-i);
                replacement.AsSpan(i,n).CopyTo(word.AsSpan(begin));Poke(tid,at,BitConverter.ToUInt64(word));i+=n;
            }
            if(!Read(address,replacement.Length).SequenceEqual(replacement))throw new IOException("Native patch readback failed.");
        });
    }
    public void WriteCameraFovClamp(ulong image,bool enable)
    {
        foreach(var f in CameraFov.Fingerprints)if(!Read(image+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes))throw new IOException("Camera FOV profile changed.");
        ReplaceCode(image,CameraFov.ClampOffset,enable?CameraFov.StandardClamp:CameraFov.UncappedClamp,enable?CameraFov.UncappedClamp:CameraFov.StandardClamp);
    }
    public void ReplaceMovementCode(ulong image,byte[] expected,byte[] replacement)
    {if(expected.Length!=14||replacement.Length!=14)throw new ArgumentException("Invalid movement patch.");ReplaceCode(image,MovementSpeed.Offset,expected,replacement);}
    public void ReplacePhotoOutputCode(ulong image,byte[] expected,byte[] replacement)
    {if(expected.Length!=PhotoCameraOutput.Original.Length)throw new ArgumentException("Invalid photo patch.");ReplaceCode(image,PhotoCameraOutput.Offset,expected,replacement);}
    public void ReplaceShoulderCode(ulong image,byte[] expected,byte[] replacement)
    {if(expected.Length!=ShoulderCamera.Original.Length)throw new ArgumentException("Invalid shoulder patch.");ReplaceCode(image,ShoulderCamera.Offset,expected,replacement);}
    public void ReplaceLootCode(ulong image,ulong offset,byte[] expected,byte[] replacement)
    {
        var original=offset==LootOverrides.DiscoveryOffset?LootOverrides.DiscoveryOriginal:offset==LootOverrides.DropOffset?LootOverrides.DropOriginal:throw new ArgumentException("Unsupported loot patch.");
        if(expected.Length!=original.Length)throw new ArgumentException("Invalid loot patch length.");ReplaceCode(image,offset,expected,replacement);
    }
    public void ReplaceProgressionCode(ulong image,ulong offset,byte[] expected,byte[] replacement)
    {
        var site=ProgressionCosts.UpgradeSites.SingleOrDefault(x=>x.Offset==offset);
        if(site.Original==null||!(expected.SequenceEqual(site.Original)&&replacement.SequenceEqual(site.Free)||expected.SequenceEqual(site.Free)&&replacement.SequenceEqual(site.Original)))throw new ArgumentException("Unsupported progression patch.");
        ReplaceCode(image,offset,expected,replacement);
    }
    public ulong FindActiveCameraStep(ulong owner,ulong image,CancellationToken cancellationToken=default)
    {
        var area=Maps().SingleOrDefault(m=>m.Start<=owner&&owner<m.End)??throw new IOException("Camera allocation unavailable.");
        var needle=BitConverter.GetBytes(image+0x5330480);var watch=Stopwatch.StartNew();
        for(var at=area.Start;at<area.End&&at-area.Start<256UL*1024*1024&&watch.Elapsed<TimeSpan.FromSeconds(15);)
        {
            cancellationToken.ThrowIfCancellationRequested();var size=(int)Math.Min(1024UL*1024,area.End-at);var bytes=Read(at,size);
            for(var i=0;i<=bytes.Length-8;i+=8)if(bytes.AsSpan(i,8).SequenceEqual(needle)&&FreeCamera.MatchesCameraStep(this,image,at+(ulong)i,owner))return at+(ulong)i;
            at+=(ulong)Math.Max(8,size-8);
        }
        throw new IOException("Active camera task not found within its validated allocation.");
    }
    public void Dispose(){lock(nativeGate){if(nativeDepth!=0)throw new InvalidOperationException("A native operation is still active.");WritesEnabled=false;disposed=true;}}
    [DllImport("libc",EntryPoint="ptrace",SetLastError=true)]private static extern nint Trace(int request,int tid,nint address,nint data);
    [DllImport("libc",EntryPoint="waitpid",SetLastError=true)]private static extern int WaitPid(int tid,out int status,int options);
}
