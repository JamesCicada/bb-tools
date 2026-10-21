using System.IO;
using System.Text;

namespace BBTools.Memory;

public sealed partial class PlayerSession
{
    public static readonly (ulong Offset,byte[] Bytes)[] PresenceFingerprints=[
        (0x14F31EB,Convert.FromHexString("4183FE107F1C488DBBA80000004C89FEE808C46C014963C666C78443A80000000000")),
        (0x14EB660,Convert.FromHexString("488D05C9FA0404488B004885C074218B8888000000"))
    ];
    public DiscordGameDetails ReadPresenceDetails(DiscordDetailPreferences choices)
    {
        if(!choices.Enabled)return new();
        ValidateImage();var player=ReadPlayer();var character=ReadCharacter();
        var manager=memory.Pointer(imageBase+0x553B130);
        if(memory.Pointer(manager+8)!=character.Address)throw new IOException("Presence character changed.");
        var deaths=choices.Deaths&&PresenceLayoutMatches(1)?BitConverter.ToUInt32(memory.Read(manager+0x88,4)):(uint?)null;
        var ng=choices.NgCycle?memory.Int32(manager+0x68):(int?)null;
        if(ng is <0 or >999)ng=null;
        var area=choices.Area?PresenceArea(unchecked((uint)memory.Int32(player.Actor+0x3F8))):null;
        var name=choices.CharacterName&&PresenceLayoutMatches(0)?DecodePresenceName(memory.Read(character.Address+0xA8,34)):null;
        EnsureSamePlayer(player);
        if(ReadCharacter().Address!=character.Address||memory.Pointer(imageBase+0x553B130)!=manager)throw new IOException("Presence character changed.");
        return new(area,choices.Level?character.Level:null,deaths,name,ng);
    }
    private bool PresenceLayoutMatches(int index){var f=PresenceFingerprints[index];return memory.Read(imageBase+f.Offset,f.Bytes.Length).SequenceEqual(f.Bytes);}
    internal static string? DecodePresenceName(byte[] bytes)
    {
        if(bytes.Length!=34)return null;
        var length=0;while(length<34&&(bytes[length]!=0||bytes[length+1]!=0))length+=2;
        if(length is 0 or >32)return null;
        try{var name=new UnicodeEncoding(false,false,true).GetString(bytes,0,length);return name.Any(c=>char.IsControl(c)||char.GetUnicodeCategory(c)==System.Globalization.UnicodeCategory.Format)?null:name;}
        catch(DecoderFallbackException){return null;}
    }
    internal static string PresenceArea(uint map)
    {
        if(map>>24==29)return "Chalice dungeon";
        return (map>>16) switch{
            0x1500=>"Hunter's Dream",0x1501=>"Abandoned Old Workshop",0x1600=>"Hemwick Charnel Lane",
            0x1700=>"Old Yharnam",0x1800=>"Cathedral Ward",0x1801=>"Central Yharnam",0x1802=>"Upper Cathedral Ward",
            0x1900=>"Forsaken Castle Cainhurst",0x1A00=>"Nightmare of Mensis",0x1B00=>"Forbidden Woods",
            0x1C00=>"Yahar'gul, Unseen Village",0x2000=>"Byrgenwerth / Lecture Building",0x2100=>"Nightmare Frontier",
            0x2200=>"Hunter's Nightmare",0x2300=>"Research Hall",0x2400=>"Fishing Hamlet",
            _=>$"Map m{map>>24:D2}_{(map>>16)&255:D2}"
        };
    }
}
