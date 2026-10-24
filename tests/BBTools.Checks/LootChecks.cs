using System.IO;
using System.Runtime.InteropServices;
using BBTools;
using BBTools.Memory;
internal static partial class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DropGetter(nint slot, nint frame);
    private static void LootChecks()
    {
        var allocation=VirtualAlloc(0,0x6000000,0x3000,4);if(allocation==0)throw new IOException("Loot fixture allocation failed.");
        var root=(ulong)allocation;
        void B(ulong at,byte[] value)=>Marshal.Copy(value,0,(nint)at,value.Length);
        void P(ulong at,ulong value)=>B(at,BitConverter.GetBytes(value));
        void I(ulong at,int value)=>B(at,BitConverter.GetBytes(value));
        try
        {
            foreach(var f in PlayerSession.Fingerprints.Concat(LootOverrides.Fingerprints))B(root+f.Offset,f.Bytes);
            var world=root+0x5700000;var actor=root+0x5710000;var modules=root+0x5720000;var stats=root+0x5730000;var record=root+0x5740000;var manager=root+0x5750000;var effects=root+0x5760000;var input=root+0x5770000;
            P(root+PlayerSession.WorldPointer,world);P(world+0x60,actor);P(actor+0x3B0,modules);P(modules+0x20,stats);P(stats+8,actor);
            I(stats+0xF8,300);I(stats+0xFC,600);I(stats+0x134,50);I(stats+0x138,100);
            P(actor+0x3C0,record);P(root+0x553B130,manager);P(manager+8,record);P(record+0x670,effects);
            foreach(var offset in new ulong[]{0x40,0x48,0x58,0x60,0x68,0x70,0x90})I(record+offset,10);I(record+0x94,1000);
            B(root+LootOverrides.DiscoveryOffset,LootOverrides.DiscoveryOriginal);B(root+LootOverrides.DropOffset,LootOverrides.DropOriginal.Concat(new byte[]{0xC3}).ToArray());
            VirtualProtect((nint)(root+(LootOverrides.DiscoveryOffset&~4095UL)),4096,0x20,out _);VirtualProtect((nint)(root+(LootOverrides.DropOffset&~4095UL)),4096,0x20,out _);
            using var memory=new ProcessMemory(Environment.ProcessId);var session=new PlayerSession(memory,root);var loot=new LootOverrides(memory,session);var before=memory.Read(record+0x40,0x58);
            var discoveryBridge=memory.AllocateCode(Convert.FromHexString("574889CFC5FA1002C5FA104A0448B8").Concat(BitConverter.GetBytes(root+LootOverrides.DiscoveryOffset)).Concat(Convert.FromHexString("FFD05FC3")).ToArray());
            var discovery=Marshal.GetDelegateForFunctionPointer<FloatGetter>((nint)discoveryBridge);
            B(input,BitConverter.GetBytes(1f).Concat(BitConverter.GetBytes(.5f)).ToArray());
            Check(discovery((nint)effects,(nint)input)==1.5f,"native discovery fixture combines baseline and equipped bonus");
            var controls=memory.AllocateData(8);
            var discoveryCode=memory.AllocateCode(LootOverrides.BuildDiscovery(root,controls));
            var dropCode=memory.AllocateCode(LootOverrides.BuildDrops(root,controls));
            byte[] Patch(ulong code,int length)=>Convert.FromHexString("FF2500000000").Concat(BitConverter.GetBytes(code)).Concat(Enumerable.Repeat((byte)0x90,length-14)).ToArray();
            var discoveryPatch=Patch(discoveryCode,LootOverrides.DiscoveryOriginal.Length);var dropPatch=Patch(dropCode,LootOverrides.DropOriginal.Length);
            memory.ReplaceLootCode(root,LootOverrides.DiscoveryOffset,LootOverrides.DiscoveryOriginal,discoveryPatch);
            memory.Write(controls,BitConverter.GetBytes(1f));Check(discovery((nint)effects,(nint)input)==2.5f,"discovery boost adds 100 points without replacing baseline or rune bonus");
            Check(discovery((nint)(effects+8),(nint)input)==1.5f,"discovery hook leaves another effect-container owner unchanged");
            I(actor+0x3F8,0x1D010000);
            Check(discovery((nint)effects,(nint)input)==2.5f,"discovery remains enabled in a chalice map context without an overworld-only filter");
            memory.Write(controls,BitConverter.GetBytes(2.5f));memory.Write(controls,BitConverter.GetBytes(2.5f));
            Check(discovery((nint)effects,(nint)input)==4f&&memory.Read(record+0x40,0x58).SequenceEqual(before),"discovery updates do not compound or edit attributes, level or Echoes");
            memory.Write(controls,new byte[8]);Check(discovery((nint)effects,(nint)input)==1.5f,"loading pause neutralizes discovery without player writes");
            memory.Write(controls,BitConverter.GetBytes(1f).Concat(BitConverter.GetBytes(1)).ToArray());memory.ReplaceLootCode(root,LootOverrides.DropOffset,LootOverrides.DropOriginal,dropPatch);
            // Native slot frame: RAX points at weight, RBP+8 is the caller.
            var bridge=memory.AllocateCode(Convert.FromHexString("55534881ECD80000004889D5488D4108C78424900000000900000048C78424A80000000000000048BA").Concat(BitConverter.GetBytes(root+LootOverrides.DropOffset)).Concat(Convert.FromHexString("FFD289D84881C4D80000005B5DC3")).ToArray());
            var drop=Marshal.GetDelegateForFunctionPointer<DropGetter>((nint)bridge);var slot=input+0x100;var frame=input+0x200;
            P(frame+8,root+0x18C5503);I(slot,1000);I(slot+4,4);I(slot+8,50);B(slot+0x15,[1]);
            Check(drop((nint)slot,(nint)frame)==59&&memory.Int32(slot+8)==50,"guaranteed enemy rolls preserve eligible item weight and quantity");
            I(slot,123456);I(slot+4,8);I(slot+8,10);
            Check(drop((nint)slot,(nint)frame)==19&&memory.Int32(slot)==123456&&memory.Read(slot+0x15,1)[0]==1,"resolved gem-type loot retains its ID, quantity and relative weight in the shared roll path");
            I(slot+8,70);B(slot+0x15,[0]);Check(drop((nint)slot,(nint)frame)==9&&memory.Int32(slot+8)==0,"guaranteed enemy rolls remove blank outcomes before native RNG selection");
            I(slot+8,20);I(slot+4,-1);B(slot+0x15,[1]);Check(drop((nint)slot,(nint)frame)==9,"unresolved items cannot remain selectable in guaranteed rolls");
            I(slot+8,70);B(slot+0x15,[0]);P(frame+8,root+1234);
            Check(drop((nint)slot,(nint)frame)==79&&memory.Int32(slot+8)==70,"chest and script callers retain their original blank chances");
            memory.ReplaceLootCode(root,LootOverrides.DiscoveryOffset,discoveryPatch,LootOverrides.DiscoveryOriginal);memory.ReplaceLootCode(root,LootOverrides.DropOffset,dropPatch,LootOverrides.DropOriginal);Check(memory.Read(root+LootOverrides.DiscoveryOffset,LootOverrides.DiscoveryOriginal.Length).SequenceEqual(LootOverrides.DiscoveryOriginal)&&memory.Read(root+LootOverrides.DropOffset,LootOverrides.DropOriginal.Length).SequenceEqual(LootOverrides.DropOriginal),"disabling loot restores both owned native sites");
            Reject<ArgumentException>(()=>loot.Apply(float.NaN,false),"reject nonfinite discovery input");Reject<ArgumentException>(()=>loot.Apply(1001,true),"reject excessive discovery input");
            Reject<ArgumentException>(()=>memory.ReplaceLootCode(root,123,new byte[21],new byte[21]),"loot writes cannot target arbitrary instructions");
            var advanced=new AdvancedSettings(DiscoveryEnabled:true,DiscoveryBonus:250,GuaranteedDrops:true);advanced.Validate();
            Check(System.Text.Json.JsonSerializer.Deserialize<AdvancedSettings>(System.Text.Json.JsonSerializer.Serialize(advanced))==advanced,"discovery and guaranteed drops roundtrip in presets");
        }
        finally{VirtualFree(allocation,0,0x8000);}
    }
    private static void LootLifecycleChecks(ProcessMemory memory,PlayerSession session,ulong root)
    {
        foreach(var f in LootOverrides.Fingerprints)memory.Write(root+f.Offset,f.Bytes);
        memory.Write(root+LootOverrides.DiscoveryOffset,LootOverrides.DiscoveryOriginal);
        memory.Write(root+LootOverrides.DropOffset,LootOverrides.DropOriginal);
        var before=session.ReadCharacter();var loot=new LootOverrides(memory,session);
        loot.Apply(100,true);loot.Apply(100,true);
        Check(memory.Read(root+LootOverrides.DiscoveryOffset,2).SequenceEqual(new byte[]{0xFF,0x25})&&memory.Read(root+LootOverrides.DropOffset,2).SequenceEqual(new byte[]{0xFF,0x25}),"loot apply installs both verified hooks in an external synthetic process");
        loot.Apply(250,false);
        Check(memory.Read(root+LootOverrides.DropOffset,LootOverrides.DropOriginal.Length).SequenceEqual(LootOverrides.DropOriginal)&&session.ReadCharacter()==before,"changing loot selection restores unselected hooks and leaves character values intact");
        loot.Restore();
        Check(memory.Read(root+LootOverrides.DiscoveryOffset,LootOverrides.DiscoveryOriginal.Length).SequenceEqual(LootOverrides.DiscoveryOriginal),"loot lifecycle restores original instructions after disconnect or disable");
    }
}
