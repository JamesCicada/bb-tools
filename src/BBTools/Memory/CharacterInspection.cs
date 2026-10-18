using System.IO;
namespace BBTools.Memory;
public sealed record InspectableCharacter(ulong Address,uint Handle,int Type,int Team)
{
    public override string ToString()=>$"Handle {Handle:X8} · type {Type} · team {Team}";
}
public sealed record CharacterInspection(InspectableCharacter Character,int Health,int Maximum,int[] Buildup,int[] Limits,int[] Effects)
{
    public string Summary=>$"HP {Health:N0} / {Maximum:N0}\nBuildup: {string.Join(" · ",Buildup.Zip(Limits,(a,b)=>$"{a}/{b}"))}\nActive effect IDs: {(Effects.Length==0?"none":string.Join(", ",Effects))}";
}
public sealed partial class PlayerSession
{
    public InspectableCharacter[] ReadNearbyCharacters()
    {
        ValidateImage();var player=ReadPlayer();var manager=memory.Pointer(imageBase+0x553B098);
        var first=BitConverter.ToUInt64(memory.Read(manager+8,8));var last=BitConverter.ToUInt64(memory.Read(manager+16,8));
        if(first==0&&last==0)return [];
        if(first<0x10000||last<first||(last-first)%8!=0||(last-first)/8>4096)throw new IOException("Invalid target bank character list.");
        var actors=memory.Read(first,checked((int)(last-first)));var result=new List<InspectableCharacter>();
        for(var at=0;at<actors.Length;at+=8)
        {
            var actor=BitConverter.ToUInt64(actors,at);if(actor==0||actor==player.Actor)continue;
            try{var modules=memory.Pointer(actor+0x3B0);var stats=memory.Pointer(modules+0x20);if(memory.Pointer(stats+8)!=actor)continue;var hp=memory.Int32(stats+0xF8);var max=memory.Int32(stats+0xFC);if(hp<0||max<=0||hp>max||max>10000000)continue;
                result.Add(new(actor,unchecked((uint)memory.Int32(actor+8)),memory.Int32(actor+0x78),memory.Int32(actor+0x88)));}
            catch(IOException){/* Native backread can retire an entry during the snapshot. */}
        }
        if(memory.Pointer(imageBase+0x553B098)!=manager||ReadPlayer().Actor!=player.Actor)throw new IOException("Character list changed during inspection.");
        return result.DistinctBy(x=>x.Address).ToArray();
    }
    public CharacterInspection InspectCharacter(InspectableCharacter selected)
    {
        if(!ReadNearbyCharacters().Contains(selected))throw new IOException("Selected character is no longer in the loaded target bank.");
        var actor=selected.Address;var modules=memory.Pointer(actor+0x3B0);var stats=memory.Pointer(modules+0x20);
        var gauges=memory.Pointer(modules+0x28);if(memory.Pointer(stats+8)!=actor||memory.Pointer(gauges+8)!=actor)throw new IOException("Character module ownership changed.");
        var values=memory.Read(gauges+0x10,40);var current=Enumerable.Range(0,5).Select(i=>BitConverter.ToInt32(values,i*4)).ToArray();var limits=Enumerable.Range(0,5).Select(i=>BitConverter.ToInt32(values,20+i*4)).ToArray();
        var effects=memory.Pointer(actor+0x1C8);if(memory.Pointer(effects+0x18)!=actor)throw new IOException("Effect list ownership changed.");
        var ids=new List<int>();var seen=new HashSet<ulong>();var node=BitConverter.ToUInt64(memory.Read(effects+8,8));
        while(node!=0){if(!seen.Add(node)||seen.Count>2048)throw new IOException("Invalid inspected effect list.");ids.Add(memory.Int32(node+0x40));node=BitConverter.ToUInt64(memory.Read(node+0x58,8));}
        if(unchecked((uint)memory.Int32(actor+8))!=selected.Handle)throw new IOException("Inspected actor was replaced.");
        return new(selected,memory.Int32(stats+0xF8),memory.Int32(stats+0xFC),current,limits,ids.ToArray());
    }
}
