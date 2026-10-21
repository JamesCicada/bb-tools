using System.Diagnostics;
using System.IO;
using System.Text.Json;
using BBTools.Memory;

internal static partial class Program
{
    // Explicit developer diagnostic. Read-only handle and wrapper prevent writes;
    // this observes native state, and does not label a stagger as a firearm parry.
    private static int SampleParry(int processId,ulong imageBase)
    {
        using var process=new ProcessMemory(processId,false);
        IMemory memory=new ReadOnlyInspectionMemory(process);
        var session=new PlayerSession(memory,imageBase);session.ValidateImage();
        var timer=Stopwatch.StartNew();string? previous=null;
        while(timer.Elapsed.TotalSeconds<45&&process.IsAlive)
        {
            try
            {
                var target=session.ReadLockedEnemy();
                if(target==null){if(previous!="unlocked"){Console.WriteLine("Waiting for lock-on");previous="unlocked";}Thread.Sleep(30);continue;}
                var snapshot=session.ReadEnemySnapshot(target);
                var modules=memory.Pointer(target.Address+0x3B0);var flags=memory.Pointer(modules+8);var tae=memory.Pointer(modules+0x18);
                if(memory.Pointer(flags)!=imageBase+0x53342C0||memory.Pointer(flags+8)!=target.Address||
                    memory.Pointer(tae)!=imageBase+0x5335190||memory.Pointer(tae+8)!=target.Address)throw new IOException("Diagnostic animation module ownership differs.");
                var data=new {snapshot.Model,target.Handle,snapshot.Health,Flags=Convert.ToHexString(memory.Read(flags+0x1BC,4)),
                    Slots=Enumerable.Range(0,10).Select(i=>new {Id=memory.Int32(tae+0x20+(ulong)i*16),Time=BitConverter.ToSingle(memory.Read(tae+0x24+(ulong)i*16,4))}).Where(x=>x.Id!=-1).ToArray(),
                    Effects=snapshot.Effects.Where(x=>x.Active).Select(x=>new{x.Id,x.State}).ToArray()};
                var key=JsonSerializer.Serialize(new {data.Model,data.Handle,data.Health,data.Flags,Ids=data.Slots.Select(x=>x.Id),data.Effects});
                if(key!=previous){Console.WriteLine($"{timer.Elapsed.TotalSeconds:0.000} {JsonSerializer.Serialize(data)}");previous=key;}
            }
            catch(IOException ex){if(previous!=ex.Message){Console.WriteLine(ex.Message);previous=ex.Message;}}
            Thread.Sleep(20);
        }
        return 0;
    }
}
