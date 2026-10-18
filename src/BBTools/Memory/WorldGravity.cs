using System.IO;
namespace BBTools.Memory;
public sealed class WorldGravity(ProcessMemory memory,PlayerSession session)
{
    public static readonly (ulong Offset,byte[] Bytes)[] Fingerprints=[(0x1E3FA45,Convert.FromHexString("C5F82881B0050000"))];
    private ulong owner;private float original,written;
    private ulong Resolve()
    {
        session.ValidateImage();foreach(var f in Fingerprints)if(!memory.Read(session.ImageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes))throw new IOException("Physics gravity reader differs from the supported build.");
        var system=memory.Pointer(session.ImageBase+0x553D700);var wrapper=memory.Pointer(system+0x28);var world=memory.Pointer(wrapper+8);
        var vector=memory.Read(world+0x5B0,12);var x=BitConverter.ToSingle(vector);var y=BitConverter.ToSingle(vector,4);var z=BitConverter.ToSingle(vector,8);
        if(x!=0||z!=0||!float.IsFinite(y)||y>=0||y<=-1000)throw new IOException("Physics gravity vector failed validation.");
        return world;
    }
    public void Apply(float scale)
    {
        if(!float.IsFinite(scale)||scale is <.05f or >2)throw new ArgumentException("World gravity must be 0.05–2.0.");
        session.ReadPlayer();if(scale==1){Restore();return;}
        var current=Resolve();if(owner!=0&&current!=owner)owner=0;
        var value=BitConverter.ToSingle(memory.Read(current+0x5B4,4));if(owner!=0&&value!=written)throw new IOException("Another tool changed world gravity. Its value was retained.");
        var baseline=owner==0?value:original;var want=baseline*scale;if(owner==current&&value==want)return;
        memory.Suspended(()=>{if(Resolve()!=current||BitConverter.ToSingle(memory.Read(current+0x5B4,4))!=value)throw new IOException("Physics world changed.");memory.Write(current+0x5B4,BitConverter.GetBytes(want));});
        original=baseline;written=want;owner=current;
    }
    public void Restore()
    {
        if(owner==0)return;
        memory.Suspended(()=>{if(Resolve()!=owner){owner=0;return;}var value=BitConverter.ToSingle(memory.Read(owner+0x5B4,4));if(value!=written)throw new IOException("World gravity changed externally; its value was retained.");memory.Write(owner+0x5B4,BitConverter.GetBytes(original));owner=0;});
    }
}
