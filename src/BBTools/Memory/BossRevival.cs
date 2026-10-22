using System.IO;
using System.Text.Json;
namespace BBTools.Memory;

public sealed record BossRevival(string Name,uint DefeatFlag,int MetOffset,uint[]? EnableFlags=null,uint[]? ClearFlags=null);
public static class BossRevivalCatalog
{
    public static BossRevival[] All =>
    [
        new("Cleric Beast",12411700,2),new("Father Gascoigne",12411800,2),new("Blood-starved Beast",12301800,2),
        new("Vicar Amelia",12401800,2),new("The Witch of Hemwick",12201800,2),new("Darkbeast Paarl",12301700,2),
        new("Shadow of Yharnam",12701800,2),new("Rom, the Vacuous Spider",13201800,2,[13200040,13200120]),new("The One Reborn",12801800,2),
        new("Martyr Logarius",12501800,2,[12500000]),new("Amygdala",13301800,2),new("Celestial Emissary",12421700,2),
        new("Ebrietas, Daughter of the Cosmos",12421800,2),new("Mergo's Wet Nurse",12601800,2),
        // m34's defeat, fog and AI events also test 9471, independently of
        // the 13401800 completion event. Reset both forms' entry/phase state.
        new("[DLC] Ludwig, the Holy Blade",13401800,1,null,[9471,..Enumerable.Range(13404800,50).Select(x=>(uint)x)]),new("[DLC] Living Failures",13501850,1,[13501200,13504220]),
        new("[DLC] Lady Maria of the Astral Clocktower",13501800,1),new("[DLC] Laurence, the First Vicar",13401850,1),new("[DLC] Orphan of Kos",13601800,1),
        new("Micolash, Host of the Nightmare",12601850,2,null,[12604731,..Enumerable.Range(12604850,50).Select(x=>(uint)x)]),
        new("Gehrman, the First Hunter",12101800,2,null,[12101850,12101852,..Enumerable.Range(12104800,12).Select(x=>(uint)x)]),
        new("Moon Presence",12101850,2,[12101800,9900],[..Enumerable.Range(12104850,32).Select(x=>(uint)x)])
    ];
    public static BossRevival Find(string name) => All.SingleOrDefault(x=>x.Name==name) ?? throw new ArgumentException("Choose a catalogued boss revival profile.");
    public static Dictionary<uint,bool> Changes(BossRevival boss,bool firstEncounter=false)
    {
        if(boss.MetOffset is <1 or >9)throw new ArgumentException("Invalid boss encounter staging offset.");
        // Clear the encounter-completed event for a first entrance. Keeping it
        // set selects repeat-fight staging (fog and the return-entry AI trigger).
        var changes=Enumerable.Range(0,10).ToDictionary(i=>boss.DefeatFlag+(uint)i,i=>!firstEncounter&&i==boss.MetOffset);
        foreach(var flag in boss.ClearFlags??[])changes[flag]=false;
        foreach(var flag in boss.EnableFlags??[])changes[flag]=true;
        return changes;
    }
}

// Bounded walk of the verified 1.09 event flag store. Only the boss allowlist uses writes.
internal sealed class EventFlagStore(IMemory memory,ulong manager)
{
    public (ulong Address,byte Mask) Locate(uint flag)
    {
        var size=unchecked((uint)memory.Int32(manager+0x1C));
        if(size is < 8 or > 65536) throw new IOException("Invalid event flag block size.");
        var block=flag/size;var bit=flag%size;var head=memory.Pointer(manager+0x38);var node=memory.Pointer(head+8);var found=head;
        var seen=new HashSet<ulong>();
        while(memory.Read(node+0x19,1)[0]==0)
        {
            if(!seen.Add(node) || seen.Count>64) throw new IOException("Invalid event flag tree.");
            var key=unchecked((uint)memory.Int32(node+0x20));
            if(key<block) node=memory.Pointer(node+0x10);
            else {found=node;node=memory.Pointer(node);}
        }
        if(found==head || unchecked((uint)memory.Int32(found+0x20))!=block) throw new IOException($"Event flag {flag} is not available in this save.");
        var kind=memory.Int32(found+0x28);ulong data;
        if(kind==2) data=memory.Pointer(found+0x30);
        else if(kind==1)
        {
            var stride=unchecked((uint)memory.Int32(manager+0x20));var index=unchecked((uint)memory.Int32(found+0x30));
            if(stride<(size+7)/8 || stride>1048576 || (ulong)stride*index>uint.MaxValue) throw new IOException("Invalid event flag pool.");
            data=memory.Pointer(manager+0x28)+(ulong)stride*index;
        }
        else throw new IOException("Unknown event flag storage.");
        return(data+(bit>>3),(byte)(0x80>>(int)(bit&7)));
    }
    public bool Read(uint flag) {var at=Locate(flag);return (memory.Read(at.Address,1)[0]&at.Mask)!=0;}
    public void Set(IReadOnlyDictionary<uint,bool> changes)
    {
        var bytes=new Dictionary<ulong,byte>();
        foreach(var (flag,value) in changes)
        {
            var at=Locate(flag);var original=bytes.TryGetValue(at.Address,out var prior)?prior:memory.Read(at.Address,1)[0];
            bytes[at.Address]=value?(byte)(original|at.Mask):(byte)(original&~at.Mask);
        }
        foreach(var (at,value) in bytes) memory.Write(at,[value]);
    }
}

public sealed partial class GameActions
{
    public string ReviveBoss(string name,string? journalFolder=null,bool firstEncounter=false)
    {
        var boss=BossRevivalCatalog.Find(name);session.ValidateImage();
        foreach(var f in ChaliceFingerprints)
            if(!memory.Read(session.ImageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes)) throw new IOException("Event flag instructions differ from the supported build.");
        void Check()
        {
            var player=session.ReadPlayer();
            if(player.Health==0)throw new IOException("Load a living character before reviving a boss.");
        }
        Check();var character=session.ReadCharacter();var manager=memory.Pointer(session.ImageBase+0x553B100);var store=new EventFlagStore(memory,manager);
        var changes=BossRevivalCatalog.Changes(boss,firstEncounter);var original=changes.Keys.ToDictionary(x=>x,store.Read);
        var folder=journalFolder??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BBTools","boss-revival");Directory.CreateDirectory(folder);
        var journal=Path.Combine(folder,$"{DateTime.UtcNow:yyyyMMddTHHmmssfffffffZ}-{boss.DefeatFlag}-{Guid.NewGuid():N}.json");
        File.WriteAllText(journal,JsonSerializer.Serialize(new {Boss=boss.Name,Version=AppInfo.DisplayVersion,CharacterAddress=character.Address,FirstEncounter=firstEncounter,Flags=original,Changes=changes},new JsonSerializerOptions{WriteIndented=true}));
        memory.Suspended(()=>
        {
            Check();var current=session.ReadCharacter();
            if(current.Address!=character.Address || memory.Pointer(session.ImageBase+0x553B100)!=manager || original.Any(x=>store.Read(x.Key)!=x.Value)) throw new IOException("Character or encounter flags changed; revival was not applied.");
            store.Set(changes);
            if(changes.Any(x=>store.Read(x.Key)!=x.Value)) throw new IOException("Revival verification failed; inspect the saved flag journal before retrying.");
        });
        return journal;
    }
}
