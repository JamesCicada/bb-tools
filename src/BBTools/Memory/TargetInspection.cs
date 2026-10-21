using System.IO;
using System.Numerics;
using System.Text;

namespace BBTools.Memory;

public sealed record EnemyGauge(string Name,int Current,int Limit)
{
    public string Summary => Limit>0 ? $"{Current:N0} / {Limit:N0}" : $"{Current:N0} · no positive limit";
    public double Percent => Limit>0?Math.Clamp(100d*Current/Limit,0,100):0;
}
public sealed record EnemyEffect(int Id,ushort? State,float Remaining,uint Flags)
{
    public bool Active => (Flags&0x800C0003)==0;
    public string Kind => State switch {2=>"Slow poison",5=>"Legacy disease",6=>"Rapid poison",116=>"Frenzy",155=>"Super-armor modifier",null=>"No parameter row",_=>$"Native state {State}"};
    public string Summary => $"Effect {Id} · {Kind} · {(Remaining>0?$"{Remaining:0.0}s":Remaining<0?"untimed":"trigger / expiring")} · {(Active?"active":"pending / inactive")}";
}
public sealed record EnemyArmor(float Current,float Limit,float Reserve,float Recovery,bool Broken)
{
    public double Percent=>Limit>0?Math.Clamp(100d*Current/Limit,0,100):0;
    public string Summary=>$"{Current:0.##} / {Limit:0.##} · {(Broken?"BROKEN":"not broken")}\nRecovery {Math.Max(0,Recovery):0.00}s · overflow reserve {Reserve:0.##}";
}
public sealed record EnemySnapshot(InspectableCharacter Character,string Model,int Health,int Maximum,int Stamina,int MaxStamina,
    EnemyGauge[] Gauges,EnemyEffect[] Effects,EnemyArmor? Armor,double? Distance,DateTimeOffset Sampled)
{
    public string Title=>$"{(string.IsNullOrWhiteSpace(Model)?"Target enemy":Model)} · {Character.Handle:X8}";
    public string Identity=>$"Type {Character.Type} · team {Character.Team}";
}
public readonly record struct InspectionContext(ulong World,ulong Player,ulong Component,uint Map);

public sealed partial class PlayerSession
{
    public static readonly (ulong Offset,byte[] Bytes)[] TargetFingerprints =
    [
        (0x188F583,Convert.FromHexString("4D8B78084D85FF0F95C049C740100000")),
        (0x188F706,Convert.FromHexString("4D8BA78000000041F6879000000020")),
        (0x1890ACD,Convert.FromHexString("80899000000020B2014989CF")),
        (0x188EC30,Convert.FromHexString("498D9221280000418A8221280000498DB22028000084C07408803E")),
        (0x1890F61,Convert.FromHexString("4C89B990000000")),
        (0x1890E48,Convert.FromHexString("498B9F880000008B436489C181E1000000F0")),
        (0x18908A9,Convert.FromHexString("488B8CD1500800004885C9745A25FF3F00003B017D51486BC038480341087447488B0048")),
        (0x1A5D9D8,Convert.FromHexString("8A4320884321C5FA115314")),
        (0x1A5DA77,Convert.FromHexString("C5FA58D2C5EA5D4310C5F82EC10F8285")),
        (0x17EAA0D,Convert.FromHexString("C5FA1017C5F82ED17608C5EA5CC0C5FA1107"))
    ];
    public InspectionContext ReadInspectionContext()
    {
        var player=ReadPlayer();return new(memory.Pointer(imageBase+WorldPointer),player.Actor,player.Component,unchecked((uint)memory.Int32(player.Actor+0x3F8)));
    }
    private void ValidateTargetProfile()
    {
        ValidateImage();
        foreach(var f in TargetFingerprints.Concat(StatusFingerprints))
            if(!memory.Read(imageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes))throw new IOException("Enemy inspection differs from the supported 1.09 layout.");
    }
    private ulong ResolveInspectionHandle(uint handle)
    {
        var selector=(handle>>14)&63;var index=handle&0x3FFF;
        if(selector>=60)throw new IOException("Target character-set selector is invalid.");
        var world=memory.Pointer(imageBase+WorldPointer);var set=memory.Pointer(world+0x850+selector*8UL);var count=memory.Int32(set);
        if(count is <1 or >16384||index>=count)throw new IOException("Target character-set entry is unavailable.");
        var entry=memory.Pointer(set+8)+index*0x38UL;var actor=memory.Pointer(entry);
        if((unchecked((uint)memory.Int32(actor+8))&0xFFFFF)!=(handle&0xFFFFF)||memory.Pointer(actor+0x18)!=entry)
            throw new IOException("Target character-set ownership changed.");
        return actor;
    }
    public InspectableCharacter? ReadLockedEnemy()
    {
        ValidateTargetProfile();var context=ReadInspectionContext();var root=memory.Pointer(imageBase+0x553E860);
        var locked=memory.Read(root+0x2820,1)[0];if(locked==0)return null;if(locked!=1)throw new IOException("Lock-on state failed validation.");
        // ChrCam's +0x90 mirror can be null even with a valid lock (native PC).
        // Read the authoritative active point list and its native selected bit.
        var first=BitConverter.ToUInt64(memory.Read(root+8,8));var node=first;ulong point=0;
        var seen=new HashSet<ulong>();
        while(node!=0)
        {
            if(node<0x10000||node%8!=0||!seen.Add(node)||seen.Count>4096)throw new IOException("Lock-on point list changed or failed validation.");
            var data=memory.Read(node+0x80,0x11);
            if((data[0x10]&0x20)!=0)
            {
                if(point!=0)throw new IOException("More than one lock-on point is selected; waiting for a stable target.");
                point=node;
            }
            node=BitConverter.ToUInt64(data);
        }
        if(point==0)return null;
        var record=memory.Pointer(point+0x88);var tagged=unchecked((uint)memory.Int32(record+0x64));
        if((tagged&0xF0000000)!=0x10000000)return null; // Ignore object/position targets.
        var actor=ResolveInspectionHandle(tagged);if(actor==context.Player)return null;
        var result=new InspectableCharacter(actor,unchecked((uint)memory.Int32(actor+8)),memory.Int32(actor+0x78),memory.Int32(actor+0x88));
        if(ReadInspectionContext()!=context||memory.Read(root+0x2820,1)[0]!=1||BitConverter.ToUInt64(memory.Read(root+8,8))!=first||(memory.Read(point+0x90,1)[0]&0x20)==0||unchecked((uint)memory.Int32(record+0x64))!=tagged)
            throw new IOException("Lock-on target changed during acquisition.");
        return result;
    }
    public EnemySnapshot ReadEnemySnapshot(InspectableCharacter selected)
    {
        ValidateTargetProfile();var context=ReadInspectionContext();var actor=selected.Address;
        if(ResolveInspectionHandle(selected.Handle)!=actor||unchecked((uint)memory.Int32(actor+8))!=selected.Handle)
            throw new IOException("Retained enemy is no longer the same character.");
        var modules=memory.Pointer(actor+0x3B0);var stats=memory.Pointer(modules+0x20);
        if(memory.Pointer(stats+8)!=actor)throw new IOException("Enemy resource module ownership changed.");
        var values=memory.Read(stats+0xF8,0x44);var hp=BitConverter.ToInt32(values);var maximum=BitConverter.ToInt32(values,4);
        if(hp<0||maximum is <1 or >100000000||hp>maximum)throw new IOException("Enemy health failed validation.");
        var stamina=BitConverter.ToInt32(values,0x3C);var maxStamina=BitConverter.ToInt32(values,0x40);
        if(stamina<0||maxStamina<0||stamina>maxStamina||maxStamina>10000000){stamina=maxStamina=0;}
        var resist=memory.Pointer(modules+0x28);if(memory.Pointer(resist)!=imageBase+0x5335900||memory.Pointer(resist+8)!=actor)throw new IOException("Enemy resistance ownership changed.");
        var gauges=memory.Read(resist+0x10,40);string[] labels=["Slow poison","Legacy disease slot","Rapid poison","Frenzy","Beasthood"];
        var meters=new EnemyGauge[5];
        for(var i=0;i<5;i++)
        {
            var current=BitConverter.ToInt32(gauges,i*4);var limit=BitConverter.ToInt32(gauges,20+i*4);
            if(current<0||limit is <0 or >10000000||current>limit)throw new IOException("Enemy status buildup changed during inspection.");
            meters[i]=new(labels[i],current,limit);
        }
        EnemyArmor? armor=null;var armorAt=BitConverter.ToUInt64(memory.Read(modules+0x48,8));
        if(armorAt!=0)
        {
            if(memory.Pointer(armorAt)!=imageBase+0x5335990||memory.Pointer(armorAt+8)!=actor)throw new IOException("Enemy super-armor ownership changed.");
            var data=memory.Read(armorAt+0x10,0x12);var floats=Enumerable.Range(0,4).Select(i=>BitConverter.ToSingle(data,i*4)).ToArray();
            if(floats.Any(x=>!float.IsFinite(x)||Math.Abs(x)>10000000)||floats[1]<0||data[16]>1)throw new IOException("Enemy super armor failed validation.");
            armor=new(floats[0],floats[1],floats[2],floats[3],data[16]!=0);
        }
        var effects=memory.Pointer(actor+0x1C8);if(memory.Pointer(effects+0x18)!=actor)throw new IOException("Enemy effect ownership changed.");
        var nodes=new List<EnemyEffect>();var seen=new HashSet<ulong>();var node=BitConverter.ToUInt64(memory.Read(effects+8,8));
        while(node!=0)
        {
            if(node<0x10000||node%8!=0||!seen.Add(node)||seen.Count>2048)throw new IOException("Enemy effect list is invalid or changed during inspection.");
            var data=memory.Read(node,0x68);var row=BitConverter.ToUInt64(data,0x48);ushort? state=row==0?null:BitConverter.ToUInt16(memory.Read(row+0x156,2));
            var remaining=BitConverter.ToSingle(data);if(!float.IsFinite(remaining))throw new IOException("Enemy effect duration failed validation.");
            nodes.Add(new(BitConverter.ToInt32(data,0x40),state,remaining,BitConverter.ToUInt32(data,0x1C)));node=BitConverter.ToUInt64(data,0x58);
        }
        string model="";
        try
        {
            var name=memory.Read(stats+0x158,0x20);var length=BitConverter.ToUInt64(name,0x10);var capacity=BitConverter.ToUInt64(name,0x18);
            if(length is >0 and <=64&&length<=capacity&&capacity<=4096)
            {var raw=capacity<=7?name[..checked((int)length*2)]:memory.Read(BitConverter.ToUInt64(name),checked((int)length*2));model=Encoding.Unicode.GetString(raw);if(model.Any(char.IsControl))model="";}
        }
        catch(IOException){ /* Optional model name; resources and effects still validate independently. */ }
        double? distance=null;
        try
        {
            Vector3 Position(ulong at){var transform=memory.Pointer(memory.Pointer(at+0x3B0)+0x68);var data=memory.Read(transform+0x1E0,12);return new(BitConverter.ToSingle(data),BitConverter.ToSingle(data,4),BitConverter.ToSingle(data,8));}
            var value=Vector3.Distance(Position(actor),Position(context.Player));if(float.IsFinite(value)&&value<100000)distance=value;
        }
        catch(IOException){ /* Missing optional transform does not discard a valid health snapshot. */ }
        if(ReadInspectionContext()!=context||ResolveInspectionHandle(selected.Handle)!=actor||unchecked((uint)memory.Int32(actor+8))!=selected.Handle||memory.Pointer(actor+0x3B0)!=modules)
            throw new IOException("Enemy context changed during inspection.");
        return new(selected,model,hp,maximum,stamina,maxStamina,meters,nodes.ToArray(),armor,distance,DateTimeOffset.UtcNow);
    }
}
