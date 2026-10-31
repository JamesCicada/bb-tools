using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BBTools.LinuxCore;

// Do not register the traced game as a System.Diagnostics.Process child: its
// Unix reaper also calls waitpid and can steal ptrace-stop notifications.
// Reap only after /proc confirms an actual zombie, never a tracing stop.
public sealed class LinuxChildProcess : IDisposable
{
    public int Id {get;}
    public StreamWriter? StandardInput {get;}
    public StreamReader? StandardOutput {get;}
    private readonly Task<int> exit;
    public bool HasExited=>exit.IsCompleted;
    public int ExitCode=>exit.IsCompletedSuccessfully?exit.Result:throw new InvalidOperationException("Child has not exited.");
    private LinuxChildProcess(int pid,int input,int output)
    {
        Id=pid;
        if(input>=0)StandardInput=new(new FileStream(new SafeFileHandle(input,true),FileAccess.Write)){AutoFlush=true};
        if(output>=0)StandardOutput=new(new FileStream(new SafeFileHandle(output,true),FileAccess.Read));
        exit=Task.Run(async()=>
        {
            while(true)
            {
                bool zombie;
                try{var stat=File.ReadAllText($"/proc/{pid}/stat");zombie=stat[(stat.LastIndexOf(')')+2)]=='Z';}
                catch(DirectoryNotFoundException){return -1;}
                catch(FileNotFoundException){return -1;}
                if(zombie)
                {
                    var result=WaitPid(pid,out var status,1);
                    if(result==pid)return (status&127)==0?(status>>8)&255:128+(status&127);
                    if(result<0&&Marshal.GetLastPInvokeError()==10)return -1;
                }
                await Task.Delay(25).ConfigureAwait(false);
            }
        });
    }
    public Task WaitForExitAsync()=>exit;
    public void Kill(){if(!HasExited&&kill(Id,9)!=0&&Marshal.GetLastPInvokeError()!=3)throw new IOException("Could not stop the child process.");}
    public void Dispose(){StandardInput?.Dispose();StandardOutput?.Dispose();}
    public static LinuxChildProcess Start(ProcessStartInfo info,bool redirect=false)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException();
        if(info.UseShellExecute||info.Arguments.Length!=0)throw new ArgumentException("Use literal ArgumentList values for native launches.");
        var allocations=new List<nint>();
        nint Strings(IEnumerable<string> strings)
        {
            var entries=strings.Select(x=>Marshal.StringToCoTaskMemUTF8(x)).ToArray();allocations.AddRange(entries);
            var array=Marshal.AllocCoTaskMem((entries.Length+1)*IntPtr.Size);allocations.Add(array);
            for(var i=0;i<entries.Length;i++)Marshal.WriteIntPtr(array,i*IntPtr.Size,entries[i]);Marshal.WriteIntPtr(array,entries.Length*IntPtr.Size,0);return array;
        }
        var actions=Marshal.AllocHGlobal(512);var initialized=false;var pipes=new List<int>();
        try
        {
            var rc=Init(actions);if(rc!=0)throw new IOException(new Win32Exception(rc).Message);initialized=true;
            if(!string.IsNullOrEmpty(info.WorkingDirectory)){rc=Chdir(actions,info.WorkingDirectory);if(rc!=0)throw new IOException(new Win32Exception(rc).Message);}
            int input=-1,output=-1;
            if(redirect)
            {
                var a=new int[2];var b=new int[2];if(pipe2(a,0x80000)!=0)throw new IOException("Could not create child input pipe.");pipes.AddRange(a);
                if(pipe2(b,0x80000)!=0)throw new IOException("Could not create child output pipe.");pipes.AddRange(b);
                if(Dup(actions,a[0],0)!=0||Dup(actions,b[1],1)!=0)throw new IOException("Could not configure child pipes.");
                foreach(var fd in pipes)if(CloseAction(actions,fd)!=0)throw new IOException("Could not close inherited child pipe.");
                input=a[1];output=b[0];
            }
            var arguments=Strings(new[]{info.FileName}.Concat(info.ArgumentList));
            var environment=Strings(info.Environment.Select(x=>x.Key+"="+x.Value));
            rc=Spawn(out var pid,info.FileName,actions,0,arguments,environment);
            if(rc!=0)throw new IOException("Could not launch game: "+new Win32Exception(rc).Message);
            foreach(var fd in pipes.Where(x=>x!=input&&x!=output))close(fd);
            pipes.Clear();return new(pid,input,output);
        }
        finally
        {
            foreach(var fd in pipes)close(fd);
            if(initialized)Destroy(actions);Marshal.FreeHGlobal(actions);foreach(var pointer in allocations)Marshal.FreeCoTaskMem(pointer);
        }
    }
    [DllImport("libc",EntryPoint="posix_spawn_file_actions_init")]private static extern int Init(nint actions);
    [DllImport("libc",EntryPoint="posix_spawn_file_actions_destroy")]private static extern int Destroy(nint actions);
    [DllImport("libc",EntryPoint="posix_spawn_file_actions_addchdir_np")]private static extern int Chdir(nint actions,string path);
    [DllImport("libc",EntryPoint="posix_spawn_file_actions_adddup2")]private static extern int Dup(nint actions,int fd,int target);
    [DllImport("libc",EntryPoint="posix_spawn_file_actions_addclose")]private static extern int CloseAction(nint actions,int fd);
    [DllImport("libc",EntryPoint="posix_spawnp")]private static extern int Spawn(out int pid,string path,nint actions,nint attributes,nint arguments,nint environment);
    [DllImport("libc",SetLastError=true)]private static extern int pipe2([Out]int[] fds,int flags);
    [DllImport("libc")]private static extern int close(int fd);
    [DllImport("libc",EntryPoint="waitpid",SetLastError=true)]private static extern int WaitPid(int pid,out int status,int flags);
    [DllImport("libc",SetLastError=true)]private static extern int kill(int pid,int signal);
}
