using System.IO;
namespace BBTools.Memory;

public sealed class CameraTuning(ProcessMemory memory,PlayerSession session)
{
    public static readonly (ulong Offset,byte[] Bytes)[] Fingerprints =
    [
        (0x143AE68,Convert.FromHexString("C5FA100EC4C17A109588010000")),
        (0x143AEEF,Convert.FromHexString("C5FA105E0C")),
    ];
    private readonly CameraFov profile=new(memory,session);
    private readonly Dictionary<ulong,(float Original,float Written)> owned=[];
    private ulong file;
    private (ulong File,ulong[] Fields) Rows(bool requirePlayer=true)
    {
        foreach(var f in Fingerprints)if(!memory.Read(session.ImageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes))throw new IOException("Camera tuning instructions differ from the supported build.");
        _=profile.Maximum;var table=profile.Resolve(requirePlayer);
        return(table.File,table.Fields.SelectMany(x=>new[]{x-0x14,x-8}).ToArray());
    }
    public void Apply(float distance,float height)
    {
        if(!float.IsFinite(distance)||!float.IsFinite(height)||distance is < .5f or > 2 || height is < .5f or >2)throw new ArgumentException("Camera distance and height scales must be 0.5–2.0.");
        var rows=Rows();
        if(file==rows.File && owned.Count==rows.Fields.Length && rows.Fields.Select((at,i)=>(at,scale:i%2==0?distance:height)).All(x=>owned.TryGetValue(x.at,out var value)&&value.Written==value.Original*x.scale&&BitConverter.ToSingle(memory.Read(x.at,4))==value.Written))return;
        memory.Suspended(()=>
        {
            rows=Rows();if(file!=rows.File){owned.Clear();file=rows.File;}
            var values=rows.Fields.Select((at,i)=>(at,scale:i%2==0?distance:height,value:BitConverter.ToSingle(memory.Read(at,4)))).ToArray();
            foreach(var x in values)
            {
                if(!float.IsFinite(x.value)||x.value is < -100 or >1000)throw new IOException("Camera tuning row failed validation.");
                if(owned.TryGetValue(x.at,out var previous)&&previous.Written!=x.value)throw new IOException("Another tool changed camera tuning. Disable before retrying.");
            }
            foreach(var x in values)
            {
                var original=owned.TryGetValue(x.at,out var previous)?previous.Original:x.value;var target=original*x.scale;
                memory.Write(x.at,BitConverter.GetBytes(target));owned[x.at]=(original,target);
            }
        });
    }
    public void Restore()
    {
        if(owned.Count==0)return;
        memory.Suspended(()=>
        {
            if(Rows(false).File!=file){owned.Clear();file=0;return;}
            foreach(var (at,value) in owned.ToArray())
            {if(BitConverter.ToSingle(memory.Read(at,4))==value.Written)memory.Write(at,BitConverter.GetBytes(value.Original));owned.Remove(at);}
            file=0;
        });
    }
}
