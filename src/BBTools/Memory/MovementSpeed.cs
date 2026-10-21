using System.IO;
namespace BBTools.Memory;

// Replace a complete 14-byte, non-RIP-relative read/multiply pair. The trampoline
// checks the current native module owner, preserves RAX/flags, and only scales
// the transient movement result for WorldChrMan's current local player.
public sealed class MovementSpeed(ProcessMemory memory,PlayerSession session)
{
    public const ulong Offset=0x1A196BB;
    public static readonly byte[] Original=Convert.FromHexString("C4C17A108574030000C5F2594B08");
    private ulong factor,code;private byte[]? installed;
    private float requestedScale=1;
    private ulong photoActor;
    public bool PhotoLocked=>photoActor!=0;
    public void Apply(float scale)
    {
        if(!float.IsFinite(scale)||scale is <.1f or >5)throw new ArgumentException("Movement speed must be 0.1–5.0.");
        session.ValidateImage();session.ReadPlayer();
        SetEffective(photoActor!=0?0:scale,photoActor);requestedScale=scale;
    }
    public void SetPhotoLock(ulong actor)
    {
        if(actor!=0&&session.ReadPlayer().Actor!=actor)throw new IOException("Photo movement owner changed.");
        SetEffective(actor!=0?0:requestedScale,actor);photoActor=actor;
    }
    private void SetEffective(float scale,ulong owner)
    {
        if(scale==1){RemoveHook();return;}
        if(installed!=null&&memory.Read(session.ImageBase+Offset,14).SequenceEqual(installed)&&BitConverter.ToSingle(memory.Read(factor,4))==scale&&BitConverter.ToUInt64(memory.Read(factor+8,8))==owner)return;
        memory.Suspended(()=>
        {
            if(installed!=null&&!memory.Read(session.ImageBase+Offset,14).SequenceEqual(installed))throw new IOException("Another tool changed movement code. Its instructions were retained.");
            if(factor==0){factor=memory.AllocateData(16);memory.Write(factor,BitConverter.GetBytes(1f));code=memory.AllocateCode(Build(session.ImageBase,factor));}
            memory.Write(factor+8,BitConverter.GetBytes(owner));
            memory.Write(factor,BitConverter.GetBytes(scale));
            if(installed==null){var patch=Convert.FromHexString("FF2500000000").Concat(BitConverter.GetBytes(code)).ToArray();memory.ReplaceMovementCode(session.ImageBase,Original,patch);installed=patch;}
        });
    }
    public void Restore()
    {
        SetEffective(photoActor!=0?0:1,photoActor);requestedScale=1;
    }
    private void RemoveHook()
    {
        if(installed==null)return;
        memory.Suspended(()=>{memory.Write(factor,BitConverter.GetBytes(1f));memory.ReplaceMovementCode(session.ImageBase,installed,Original);installed=null;});
    }
    internal static byte[] Build(ulong imageBase,ulong factor)
    {
        using var stream=new MemoryStream();using var w=new BinaryWriter(stream);var jumps=new List<long>();
        void B(string hex)=>w.Write(Convert.FromHexString(hex));void Skip(string op){B(op);jumps.Add(stream.Position);w.Write(0);}
        w.Write(Original);B("9C5048B8");w.Write(imageBase+PlayerSession.WorldPointer);B("488B004885C0");Skip("0F84");
        B("488B406049394508");Skip("0F85");B("48B8");w.Write(factor);
        // A photo lock belongs to its captured hunter; ordinary speed has no bound owner.
        B("4883780800");B("740E");B("488B400849394508");Skip("0F85");
        B("48B8");w.Write(factor);B("C5FA5900");
        var done=stream.Position;B("589DFF2500000000");w.Write(imageBase+Offset+14);
        var end=stream.Position;foreach(var at in jumps){stream.Position=at;w.Write((int)(done-at-4));}stream.Position=end;return stream.ToArray();
    }
}
