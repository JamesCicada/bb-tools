using System.Globalization;

namespace BBTools;

public sealed record DiscordDetailPreferences(bool Enabled=false,bool Area=true,bool Level=true,bool Deaths=true,bool CharacterName=true,bool NgCycle=false);

public sealed record DiscordGameDetails(string? Area=null,int? Level=null,uint? Deaths=null,string? CharacterName=null,int? NgCycle=null)
{
    internal string DetailsText=>Limit(string.Join(" · ",new[]{"Bloodborne",Clean(CharacterName),Level is {} level?$"BL {level}":null,NgCycle is {} ng?(ng==0?"NG":$"NG+{ng}"):null}.Where(x=>!string.IsNullOrWhiteSpace(x))));
    internal string StateText(string runtime)=>Limit(string.Join(" · ",new[]{Clean(Area),Deaths is {} deaths?$"Deaths: {deaths}":null,runtime}.Where(x=>!string.IsNullOrWhiteSpace(x))));
    private static string? Clean(string? text)=>text==null?null:string.Concat(text.Where(c=>!char.IsControl(c)&&CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.Format)).Trim();
    private static string Limit(string value)
    {
        if(value.Length<=128)return value;
        var end=char.IsHighSurrogate(value[127])?127:128;return value[..end];
    }
}
