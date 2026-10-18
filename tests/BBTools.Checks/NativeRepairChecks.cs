using System.Runtime.InteropServices;
using BBTools.Memory;
internal static partial class Program
{
    private static void NativeRepairChecks()
    {
        var allocation=VirtualAlloc(0,0x6000000,0x3000,4);if(allocation==0)throw new Exception("Native repair fixture allocation failed.");
        var root=(ulong)allocation;void Bytes(ulong at,byte[] bytes)=>Marshal.Copy(bytes,0,(nint)at,bytes.Length);
        void Int(ulong at,int value)=>Bytes(at,BitConverter.GetBytes(value));void Ptr(ulong at,ulong value)=>Bytes(at,BitConverter.GetBytes(value));
        var data=root+0x5800000;var equip=data+0x1D0;var payload=root+0x5810000;var row=root+0x5820000;var code=root+0x5830000;var bridge=root+0x5840000;
        try
        {
            Bytes(root+0x1A89070,Convert.FromHexString("8B07894710C3"));
            Bytes(root+0x1F29AA0,Convert.FromHexString("48B8").Concat(BitConverter.GetBytes(row)).Concat(Convert.FromHexString("48894708C3")).ToArray());
            Bytes(root+0x14D26D0,Convert.FromHexString("4863C68B848700040000C3"));
            Bytes(root+0x14D24E0,Convert.FromHexString("4863C68B8C870004000001D1898C8700040000B801000000C3"));
            foreach(var at in new ulong[]{0x1A89000,0x1F29000,0x14D2600,0x14D2400})VirtualProtect((nint)(root+(at&~4095UL)),4096,0x20,out _);
            Bytes(row+0xBE,BitConverter.GetBytes((ushort)200));for(var i=0;i<8;i++)Int(equip+0x94+(ulong)i*4,-1);
            // Two hands in different slots, with independent current values.
            Int(equip+0x94,7000000);Int(equip+0x94+4*4,14000000);Int(equip+0x400,40);Int(equip+0x400+4*4,140);
            Ptr(payload,data);Int(payload+8,30);Bytes(code,GameActions.BuildRepairHelper(root));
            var wrapper=Convert.FromHexString("57564883EC284889CF48B8").Concat(BitConverter.GetBytes(code)).Concat(Convert.FromHexString("FFD04883C4285E5FC3")).ToArray();Bytes(bridge,wrapper);
            VirtualProtect((nint)code,4096,0x20,out _);VirtualProtect((nint)bridge,4096,0x20,out _);var call=Marshal.GetDelegateForFunctionPointer<Getter>((nint)bridge);call((nint)payload);
            Check(Marshal.ReadInt32((nint)(equip+0x400))==200&&Marshal.ReadInt32((nint)(equip+0x410))==140,"native threshold repair repairs damaged right-hand slot and preserves left hand above threshold");
            Check(Marshal.ReadInt32((nint)(payload+32))==7000000&&Marshal.ReadInt32((nint)(payload+32+4*24))==14000000,"native repair reports both hands without relying on external inventory cache lookup");
            Int(payload+8,100);call((nint)payload);Check(Marshal.ReadInt32((nint)(equip+0x410))==200,"native repair-now restores both hand slots through the game setter");
            Check(Marshal.ReadInt32((nint)(payload+32+4*24+8))==200,"native repair verifies post-setter durability in its per-slot report");
        }
        finally{VirtualFree(allocation,0,0x8000);}
    }
}
