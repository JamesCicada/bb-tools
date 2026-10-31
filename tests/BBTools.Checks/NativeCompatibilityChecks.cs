using System.IO;
using System.Runtime.InteropServices;
using BBTools.Memory;

internal static partial class Program
{
    private sealed class ObserverMemory : IExecutableMemory
    {
        public readonly Dictionary<ulong,byte[]> Blocks=[];
        public readonly HashSet<ulong> Data=[];
        public Action<ulong>? BeforeRead;
        public byte[] Read(ulong address,int size)
        {
            BeforeRead?.Invoke(address);
            foreach(var (at,bytes) in Blocks)
                if(address>=at && address-at<=(ulong)bytes.Length && (ulong)size<=(ulong)bytes.Length-(address-at)) return bytes.AsSpan((int)(address-at),size).ToArray();
            throw new IOException("Unmapped synthetic instruction address.");
        }
        public void Write(ulong address,byte[] value)=>throw new Exception("Compatibility validation must never write.");
        public bool IsExecutable(ulong address,int size)=>Blocks.Any(p=>!Data.Contains(p.Key)&&address>=p.Key&&address-p.Key+(ulong)size<=(ulong)p.Value.Length);
    }
    // Independent machine-code fixture for the documented observer ABI.
    private static byte[] ObserverFixture(ulong stub,ulong entry,byte[] original,ulong callback)
    {
        var bytes=new List<byte>();
        void B(string hex)=>bytes.AddRange(Convert.FromHexString(hex));
        B("488DA42480FFFFFF5051525657415041514152415341544989E44883E4F04881EC80000000");
        for(var reg=0;reg<8;reg++)bytes.AddRange([0xF3,0x0F,0x7F,(byte)(0x44+reg*8),0x24,(byte)(reg*16)]);
        B("48B8");bytes.AddRange(BitConverter.GetBytes(callback));B("FFD0");
        for(var reg=0;reg<8;reg++)bytes.AddRange([0xF3,0x0F,0x6F,(byte)(0x44+reg*8),0x24,(byte)(reg*16)]);
        B("4C89E4415C415B415A415941585F5E5A5958488DA42480000000");
        bytes.AddRange(original.Take(13));B("E9");bytes.AddRange(BitConverter.GetBytes(checked((int)((long)(entry+13)-(long)(stub+(ulong)bytes.Count+4)))));
        return bytes.ToArray();
    }
    private static byte[] ObserverEntry(ulong entry,ulong stub,byte[] original)
    {
        var patched=original.ToArray();patched[0]=0xE9;
        BitConverter.GetBytes(checked((int)((long)stub-(long)(entry+5)))).CopyTo(patched,1);
        Array.Fill(patched,(byte)0x90,5,8);return patched;
    }
    private static void NativeCompatibilityChecks()
    {
        const ulong image=0x800000000,stub=image+0x5CF0DF0,callback=0x7FF000100000;
        var memory=new ObserverMemory();
        foreach(var f in GameActions.Fingerprints.Concat(GameActions.MenuFingerprints))memory.Blocks[image+f.Offset]=f.Bytes.ToArray();
        var item=GameActions.Fingerprints.Single(x=>x.Offset==0x17D89F0);
        var menu=GameActions.MenuFingerprints.Single(x=>x.Offset==0x1BB7F60);
        memory.Blocks[callback]=[0xC3];
        void Hook((ulong Offset,byte[] Bytes) f)
        {
            memory.Blocks[stub]=ObserverFixture(stub,image+f.Offset,f.Bytes,callback);
            memory.Blocks[image+f.Offset]=ObserverEntry(image+f.Offset,stub,f.Bytes);
        }
        Hook(item);
        Check(NativeCodeCompatibility.Matches(memory,image,item.Offset,item.Bytes),"known item observer ABI accepts the reported E9 plus eight NOPs without changing runtime code");
        GameActions.ValidateActionInstructions(memory,image,GameActionKind.Item);
        GameActions.ValidateActionInstructions(memory,image,GameActionKind.Gem);
        Check(true,"item and gem requests both accept the verified observer wrapper");
        var entryReads=0;
        memory.BeforeRead=at=>{if(at==image+item.Offset && ++entryReads==2)memory.Blocks[at][1]^=1;};
        Check(!NativeCodeCompatibility.Matches(memory,image,item.Offset,item.Bytes),"observer entry changes during inspection are rejected rather than caching earlier acceptance");
        memory.BeforeRead=null;Hook(item);
        var good=memory.Blocks[stub].ToArray();var rejected=true;
        for(var i=0;i<good.Length-4;i++)
        {
            if(i>=87&&i<95)continue;
            memory.Blocks[stub]=good.ToArray();memory.Blocks[stub][i]^=1;
            rejected &= !NativeCodeCompatibility.Matches(memory,image,item.Offset,item.Bytes);
        }
        Check(rejected,"every changed fixed wrapper byte is rejected, including argument preservation and original prologue");
        memory.Blocks[stub]=good.ToArray();memory.Data.Add(stub);
        Check(!NativeCodeCompatibility.Matches(memory,image,item.Offset,item.Bytes),"readable non-executable trampolines cannot pass observer validation");
        memory.Data.Clear();memory.Data.Add(callback);
        Check(!NativeCodeCompatibility.Matches(memory,image,item.Offset,item.Bytes),"observer callbacks must point to executable memory");
        memory.Data.Clear();memory.Blocks[stub]=good.ToArray();memory.Blocks[stub][^1]^=1;
        Check(!NativeCodeCompatibility.Matches(memory,image,item.Offset,item.Bytes),"observer must resume at the exact original instruction boundary");
        memory.Blocks[stub]=good.ToArray();memory.Blocks[image+item.Offset][6]=0xCC;
        Check(!NativeCodeCompatibility.Matches(memory,image,item.Offset,item.Bytes),"unknown entry padding is rejected rather than accepting any E9");
        Hook(item);memory.Blocks.Remove(stub);
        Check(!NativeCodeCompatibility.Matches(memory,image,item.Offset,item.Bytes),"missing wrapper memory fails closed");
        memory.Blocks[image+item.Offset]=item.Bytes.ToArray();Hook(menu);
        GameActions.ValidateMenuInstructions(memory,image,GameActions.Menus.Single(x=>x.Factory==menu.Offset));
        Check(true,"selected level-up factory accepts the audited observer ABI");
        memory.Blocks[image+menu.Offset][0]=0xCC;
        GameActions.ValidateMenuInstructions(memory,image,GameActions.Menus.Single(x=>x.Name=="Storage"));
        Check(true,"unrelated level-up changes no longer block Storage");
        Reject<IOException>(()=>GameActions.ValidateMenuInstructions(memory,image,GameActions.Menus.Single(x=>x.Factory==menu.Offset)),"unknown selected menu hooks remain blocked");
        memory.Blocks[image+GameActions.MenuFingerprints[1].Offset][0]=0xCC;
        Reject<IOException>(()=>GameActions.ValidateMenuInstructions(memory,image,GameActions.Menus.Single(x=>x.Name=="Storage")),"menu scope still validates shared attach code");
        var foreign=GameActions.Fingerprints.Single(x=>x.Offset==0x1A86FF0);Hook(foreign);
        Check(!NativeCodeCompatibility.Matches(memory,image,foreign.Offset,foreign.Bytes),"observer compatibility cannot expand to unaudited entry points");
        NativeObserverExecutionCheck(item.Bytes);
    }
    private static void NativeObserverExecutionCheck(byte[] expected)
    {
        var allocation=VirtualAlloc(0,0x6000000,0x3000,4);if(allocation==0)throw new Exception("Observer fixture allocation failed.");
        var image=(ulong)allocation;var entry=image+0x17D89F0;var stub=image+0x5700000;var callback=stub+0x1000;var harness=callback+0x1000;
        try
        {
            void Put(ulong at,byte[] bytes)=>Marshal.Copy(bytes,0,(nint)at,bytes.Length);
            var body=Convert.FromHexString("554889E54157415641554154534881EC280000004889F84881C4280000005B415C415D415E415F5DC3");
            Put(entry,body);Put(entry,ObserverEntry(entry,stub,expected));Put(stub,ObserverFixture(stub,entry,expected,callback));
            Put(callback,Convert.FromHexString("48C7C7EFBE0000C3"));
            Put(harness,Convert.FromHexString("574883EC204889CF48B8").Concat(BitConverter.GetBytes(entry)).Concat(Convert.FromHexString("FFD04883C4205FC3")).ToArray());
            foreach(var at in new[]{entry,stub,callback,harness})VirtualProtect((nint)at,4096,0x20,out _);
            using var process=new ProcessMemory(Environment.ProcessId,false);
            Check(NativeCodeCompatibility.Matches(process,image,0x17D89F0,expected),"Windows executable-page inspection accepts a valid relocated synthetic observer");
            var invoke=Marshal.GetDelegateForFunctionPointer<Getter>((nint)harness);
            Check(invoke((nint)0x12345678)==(nint)0x12345678,"native observer executes its callback, restores the argument and resumes the original grant entry");
        }
        finally{VirtualFree(allocation,0,0x8000);}
    }
}
