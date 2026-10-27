using System.IO;
namespace BBTools.Memory;

// Borrow a private frame only for the renderer read. Never put VirtualAlloc
// memory in the controller's owning +D0 slot: its destructor frees that slot.
public sealed class PhotoCameraOutput(ProcessMemory memory, PlayerSession session)
{
    public const ulong Offset=0x193BEC5;
    public static readonly byte[] Original=Convert.FromHexString("498B851001000083B8C4000000007464488B80D00000004885C07458");
    private ulong controls,code;private byte[]? installed;
    public bool RestorationPending=>installed!=null;
    public bool Matches()=>installed!=null&&memory.Read(session.ImageBase+Offset,Original.Length).SequenceEqual(installed);
    public void Apply(ulong frame,ulong controller,ulong actor,ulong source,uint handle)
    {
        memory.Suspended(()=>
        {
            if(controls==0){controls=memory.AllocateData(48);code=memory.AllocateCode(Build(session.ImageBase,controls));}
            if(installed==null)
            {
                installed=Convert.FromHexString("FF2500000000").Concat(BitConverter.GetBytes(code)).Concat(Enumerable.Repeat((byte)0x90,Original.Length-14)).ToArray();
                memory.ReplacePhotoOutputCode(session.ImageBase,Original,installed);
            }
            else if(!Matches())throw new IOException("Another tool changed the photo output hook.");
            memory.Write(controls+8,BitConverter.GetBytes(controller));memory.Write(controls+16,BitConverter.GetBytes(actor));
            memory.Write(controls+24,BitConverter.GetBytes(source));memory.SetInt32(controls+32,unchecked((int)handle));
            memory.Write(controls,BitConverter.GetBytes(frame));
        });
    }
    public void Restore()
    {
        if(installed==null)return;
        memory.Suspended(()=>
        {
            memory.Write(controls,new byte[8]);
            var current=memory.Read(session.ImageBase+Offset,Original.Length);
            if(current.SequenceEqual(installed))memory.ReplacePhotoOutputCode(session.ImageBase,installed,Original);
            else if(!current.SequenceEqual(Original))throw new IOException("Photo output instructions changed externally; their replacement was preserved.");
            installed=null;
        });
    }
    internal static byte[] Build(ulong image,ulong controls)
    {
        using var stream=new MemoryStream();using var w=new BinaryWriter(stream);var fallback=new List<long>();
        void B(string hex)=>w.Write(Convert.FromHexString(hex));
        void Skip(string op){B(op);fallback.Add(stream.Position);w.Write(0);}
        void Jump(ulong at){B("FF2500000000");w.Write(at);}
        B("9C41524153498B851001000049BB");w.Write(controls);
        B("49833B00");Skip("0F84");B("49394308");Skip("0F85");
        B("49BA");w.Write(image+PlayerSession.WorldPointer);B("4D8B124D85D2");Skip("0F84");
        B("4D8B52604D395310");Skip("0F85");B("458B5208453B5320");Skip("0F85");
        B("49BA");w.Write(image+0x553E860);B("4D8B124D85D2");Skip("0F84");
        B("4D8B92302800004D395318");Skip("0F85");B("4D399528010000");Skip("0F85");
        B("498B03415B415A9D4885C0");Jump(image+0x193BEE1);
        var fallbackAt=stream.Position;
        B("415B415A9D498B851001000083B8C4000000000F84");var noMode=stream.Position;w.Write(0);
        B("488B80D00000004885C00F84");var noFrame=stream.Position;w.Write(0);Jump(image+0x193BEE1);
        var normal=stream.Position;Jump(image+0x193BF39);var end=stream.Position;
        foreach(var at in fallback){stream.Position=at;w.Write((int)(fallbackAt-at-4));}
        foreach(var at in new[]{noMode,noFrame}){stream.Position=at;w.Write((int)(normal-at-4));}
        stream.Position=end;return stream.ToArray();
    }
}
