using System.Runtime.InteropServices;
using BBTools.Memory;
internal static partial class Program
{
    private static void PhotoOutputChecks()
    {
        var allocation=VirtualAlloc(0,0x6000000,0x3000,4);if(allocation==0)throw new Exception("Photo output fixture allocation failed.");
        var root=(ulong)allocation;var task=root+0x5700000;var controller=root+0x5710000;var actor=root+0x5720000;
        var world=root+0x5730000;var camera=root+0x5740000;var source=root+0x5750000;var frame=root+0x5760000;var controls=root+0x5770000;
        void Bytes(ulong at,byte[] b)=>Marshal.Copy(b,0,(nint)at,b.Length);
        void Ptr(ulong at,ulong value)=>Bytes(at,BitConverter.GetBytes(value));
        void Int(ulong at,int value)=>Bytes(at,BitConverter.GetBytes(value));
        try
        {
            Ptr(root+PlayerSession.WorldPointer,world);Ptr(world+0x60,actor);Int(actor+8,123);
            Ptr(root+0x553E860,camera);Ptr(camera+0x2830,source);Ptr(task+0x110,controller);Ptr(task+0x128,source);
            Ptr(controls,frame);Ptr(controls+8,controller);Ptr(controls+16,actor);Ptr(controls+24,source);Int(controls+32,123);
            Bytes(root+0x193BEE1,[0xC3]);Bytes(root+0x193BF39,[0x31,0xC0,0xC3]);VirtualProtect((nint)(root+0x193B000),4096,0x20,out _);
            using var memory=new ProcessMemory(Environment.ProcessId);var hook=memory.AllocateCode(PhotoCameraOutput.Build(root,controls));
            var wrapper=Convert.FromHexString("41554989CD48B8").Concat(BitConverter.GetBytes(hook)).Concat(Convert.FromHexString("FFD0415DC3")).ToArray();
            var call=Marshal.GetDelegateForFunctionPointer<Getter>((nint)memory.AllocateCode(wrapper));
            Check((ulong)call((nint)task)==frame&&Marshal.ReadInt64((nint)(controller+0xD0))==0&&Marshal.ReadInt32((nint)(controller+0xC4))==0,
                "photo output borrows the private frame with native ownership and camera mode untouched");
            Ptr(controls,0);Check(call((nint)task)==0,"disabled photo output follows the native normal-camera branch");
            Int(controller+0xC4,1);Ptr(controller+0xD0,source);
            Check((ulong)call((nint)task)==source,"disabled photo hook preserves another native debug camera's frame");
            Ptr(controls,frame);Int(actor+8,124);
            Check((ulong)call((nint)task)==source,"photo render hook rejects a recycled actor handle before borrowing memory");
            Int(actor+8,123);Ptr(camera+0x2830,source+16);
            Check((ulong)call((nint)task)==source,"photo render hook falls back when the camera source changes during loading");
            Ptr(camera+0x2830,source);Ptr(world+0x60,0);
            Check((ulong)call((nint)task)==source&&Marshal.ReadInt64((nint)(controller+0xD0))==(long)source,
                "null-player photo cleanup cannot expose a tool allocation to the native camera destructor");
        }
        finally{VirtualFree(allocation,0,0x8000);}
    }
}
